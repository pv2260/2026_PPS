// Percep PD — Clinician Panel. Vanilla JS SPA, no build step.
//
// Talks to the Unity headset through:
//   - REST  /api/...   (commands + reads)
//   - WS    /ws        (live event stream)
//
// All event payloads use the WsEnvelope { type, payload, serverTime } format
// from NetworkMessages.cs. The payload string is JSON — parse on demand.
//
// Two independent Unity scenes are run in sequence per participant:
//   Task1Pps        — PPS / vibrotactile looming
//   Task2HitOrMiss  — Hit-or-Miss collision judgment
// The panel detects which scene is currently hosting the server via
// status.taskKind and (a) shows it in the header, (b) reveals the matching
// task parameters, (c) resets all live state when the scene changes so that
// Task 2's Live view never inherits Task 1's counters.

(() => {
  const $  = (sel, root = document) => root.querySelector(sel);
  const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

  const TASK_META = {
    Task1Pps:       { label: 'PPS / vibrotactile', badge: 'is-task1', start: 'Start PPS session' },
    Task2HitOrMiss: { label: 'Hit-or-Miss',        badge: 'is-task2', start: 'Start Hit-or-Miss session' },
  };

  const state = {
    connected: false,
    status: null,
    correctCount: 0,
    totalScored: 0,
    socket: null,
    // Which Unity scene we currently believe is loaded. Used to detect a
    // scene switch (Task 1 -> Task 2) and reset live state on transition.
    currentTaskKind: null,
    // Aggressive reconnect cadence so a page opened just before Unity finishes
    // loading the scene snaps online fast. Backoff grows and is capped below.
    reconnectMs: 250,
  };

  const WS_RECONNECT_MIN_MS = 250;
  const WS_RECONNECT_MAX_MS = 3000;

  // ---- Tabs ----
  $$('.tab').forEach(btn => btn.addEventListener('click', () => {
    $$('.tab').forEach(b => b.classList.toggle('active', b === btn));
    const target = btn.dataset.tab;
    $$('.tab-panel').forEach(p => p.classList.toggle('active', p.id === target));
    if (target === 'sessions') refreshSessions();
  }));

  // ---- Footer ----
  $('#serverUrl').textContent = location.host;

  // ---- Form: prefill date ----
  const today = new Date().toISOString().slice(0, 10);
  $('input[name=sessionDate]').value = today;

  // ---- Task 1 trial-count total ----
  // Live "trials per block" totals, plus the Task 1 design-cell check.
  //
  // Task 1 counts are REJECTED by PpsTaskAsset.ApplyCount unless each is
  // divisible by its design cells, and a rejected value silently runs the asset
  // default instead. So the panel enforces it here rather than letting the
  // clinician type a number that will be quietly discarded.
  //
  // Cells assume the asset's width factor is OFF (ActiveWidths = 1):
  //   VT = distances(7) x speeds(2) x widths(1) = 14
  //   V  =               speeds(2) x widths(1) =  2
  //   T  = distances(7) x speeds(2)            = 14   (T never crosses width)
  // Defaults match a 7-distance asset with the width factor off. They are
  // OVERWRITTEN from server status (task1*DesignCells) as soon as the headset
  // connects, so lowering DistanceStageCount for a short test run immediately
  // relaxes the panel to match.
  const TASK1_CELLS = { task1VtTrialsPerBlock: 14, task1VisualOnlyTrialsPerBlock: 2, task1TactileOnlyTrialsPerBlock: 14 };

  // Applies live cell sizes from the headset and refreshes the inputs' step
  // attributes so the browser's own validation tracks the real asset.
  function applyDesignCells(s) {
    if (!s) return;
    const map = {
      task1VtTrialsPerBlock: s.task1VtDesignCells,
      task1VisualOnlyTrialsPerBlock: s.task1VisualOnlyDesignCells,
      task1TactileOnlyTrialsPerBlock: s.task1TactileOnlyDesignCells,
    };
    let changed = false;
    Object.keys(map).forEach((name) => {
      const cells = map[name];
      if (!cells || cells < 1 || cells === TASK1_CELLS[name]) return;
      TASK1_CELLS[name] = cells;
      changed = true;
    });
    if (!changed) return;

    const form = $('#sessionForm');
    Object.keys(TASK1_CELLS).forEach((name) => {
      const el = form?.querySelector(`[name="${name}"]`);
      if (!el) return;
      el.step = String(TASK1_CELLS[name]);
      const hint = el.parentElement?.querySelector('small');
      if (hint) hint.textContent = `multiple of ${TASK1_CELLS[name]}`;
    });
    renderTask1Warning();
  }

  bindTotals();

  function bindTotals() {
    const form = $('#sessionForm');
    if (!form) return;

    const toInt = (el) => {
      const v = parseInt(el.value, 10);
      return isNaN(v) ? 0 : Math.max(0, v);
    };
    const bind = (names, outId, after) => {
      const els = names.map(n => form.querySelector(`[name="${n}"]`));
      const out = $(outId);
      if (!out || els.some(e => !e)) return;
      const update = () => {
        out.textContent = els.reduce((s, e) => s + toInt(e), 0);
        if (after) after(els);
      };
      els.forEach(e => e.addEventListener('input', update));
      update();
    };

    bind(Object.keys(TASK1_CELLS), '#task1TrialTotal', () => renderTask1Warning());
    bind(['task2ClearHitTrialsPerBlock', 'task2NearHitTrialsPerBlock', 'task2NearMissTrialsPerBlock',
          'task2ClearMissTrialsPerBlock', 'task2GreyZoneTrialsPerBlock'], '#task2TrialTotal');
  }

  // Returns [] when every Task 1 count is usable, else a list of problems.
  function task1CountErrors() {
    const form = $('#sessionForm');
    if (!form) return [];
    const labels = {
      task1VtTrialsPerBlock: 'VT',
      task1VisualOnlyTrialsPerBlock: 'visual-only',
      task1TactileOnlyTrialsPerBlock: 'tactile-only',
    };
    const out = [];
    Object.keys(TASK1_CELLS).forEach((name) => {
      const el = form.querySelector(`[name="${name}"]`);
      if (!el) return;
      const v = parseInt(el.value, 10);
      if (isNaN(v) || v === 0) return; // 0 = use protocol default, always valid
      const cells = TASK1_CELLS[name];
      if (v % cells !== 0) {
        const lo = Math.max(cells, Math.floor(v / cells) * cells);
        out.push(`${labels[name]} ${v} is not a multiple of ${cells} (nearest: ${lo} or ${lo + cells})`);
      }
    });
    return out;
  }

  function renderTask1Warning() {
    const warn = $('#task1CountWarn');
    if (!warn) return;
    const errs = task1CountErrors();
    warn.hidden = errs.length === 0;
    warn.textContent = errs.length ? `Will be rejected — ${errs.join('; ')}.` : '';
  }

  // ---- Start session ----
  $('#sessionForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    $('#startError').textContent = '';

    // Task 1 counts that fail the design-cell check are discarded by Unity and
    // the asset default runs instead. Refuse to start rather than run a
    // protocol the clinician did not choose.
    if (state.currentTaskKind !== 'Task2HitOrMiss') {
      const errs = task1CountErrors();
      if (errs.length) {
        $('#startError').textContent = `Fix the Task 1 trial counts first — ${errs.join('; ')}.`;
        return;
      }
    }

    const fd = new FormData(e.target);
    const ck = (name) => e.target.querySelector(`[name="${name}"]`)?.checked || false;
    const csv = (name) => (fd.get(name) || '').split(',').map(s => s.trim()).filter(Boolean);
    const num = (name, fallback = 0) => {
      const v = parseFloat(fd.get(name));
      return isNaN(v) ? fallback : v;
    };
    const int = (name, fallback = 0) => {
      const v = parseInt(fd.get(name), 10);
      return isNaN(v) ? fallback : v;
    };
    const metadata = {
      // Identity
      participantId:     fd.get('participantId'),
      clinicianInitials: fd.get('clinicianInitials') || '',
      sessionDate:       fd.get('sessionDate'),
      sessionId:         '',
      sessionNumber:     int('sessionNumber', 1),

      // Subject
      ageYears:          int('ageYears', 0),
      dominantHand:      int('dominantHand', 0),
      heightCm:          num('heightCm', 170),
      shoulderWidthCm:   num('shoulderWidthCm', 42),
      subjectGroup:      fd.get('subjectGroup') || 'healthy',
      hasDbs:            ck('hasDbs'),

      // Session condition
      sessionType:       int('sessionType', 2),
      dbsStatus:         int('dbsStatus', 0),
      language:          fd.get('language') || 'english',

      // Equipment
      eegEnabled:           ck('eegEnabled'),
      emgEnabled:           ck('emgEnabled'),
      heartRateBandEnabled: ck('heartRateBandEnabled'),
      eyeTrackingEnabled:   ck('eyeTrackingEnabled'),

      // Task 1 parameters (defaults verified against PpsTaskAsset:
      // 3 blocks, VT 70 + V 28 + T 56 = 154 trials/block, 30 s rest)
      task1NumberOfBlocks:           int('task1NumberOfBlocks', 3),
      task1VtTrialsPerBlock:         int('task1VtTrialsPerBlock', 70),
      task1VisualOnlyTrialsPerBlock: int('task1VisualOnlyTrialsPerBlock', 28),
      task1TactileOnlyTrialsPerBlock:int('task1TactileOnlyTrialsPerBlock', 56),
      task1TrialsPerBlock:
        int('task1VtTrialsPerBlock', 70) +
        int('task1VisualOnlyTrialsPerBlock', 28) +
        int('task1TactileOnlyTrialsPerBlock', 56),
      task1BreakDurationSeconds:  num('task1BreakDurationSeconds', 30),
      // Loom velocities in m/s. The PPS asset now stores speed natively, so the
      // value is used as typed with no conversion. Blank -> 0 -> keep the asset
      // value, which is how a control run gets the exact protocol speed.
      task1FastSpeedMps:          num('task1FastSpeedMps', 0),
      task1SlowSpeedMps:          num('task1SlowSpeedMps', 0),
      // Crosshair height is no longer a panel field: the anchor is derived from
      // the headset, so eye level adapts per participant automatically. Sent as
      // 0 so the asset value always wins.
      task1CrosshairHeightM:      0,
      // Narrow/wide offsets removed from the form — light width is derived
      // from shoulderWidthCm. Left at 0 in the record; setup.json values are
      // overwritten from the task asset by the controller anyway.
      task1NarrowOffsetCm:        0,
      task1WideOffsetCm:          0,
      // Protocol constants, recorded for the setup.json snapshot; no longer
      // form inputs since they are not per-session choices.
      task1LoomingSpeeds:         ['slow', 'fast'],
      task1PracticeVtOnlyTrials:  2,
      task1PracticeVtVisualTrials: 4,

      // Task 2 parameters
      task2NumberOfBlocks:        int('task2NumberOfBlocks', 3),
      // Per-category counts. Requires the per-category branch in
      // ApplyTask2SessionOverrides (see speed-overrides-csharp.md); the legacy
      // flat-total split must be removed or it will fight these.
      task2ClearHitTrialsPerBlock:  int('task2ClearHitTrialsPerBlock', 0),
      task2NearHitTrialsPerBlock:   int('task2NearHitTrialsPerBlock', 0),
      task2NearMissTrialsPerBlock:  int('task2NearMissTrialsPerBlock', 0),
      task2ClearMissTrialsPerBlock: int('task2ClearMissTrialsPerBlock', 0),
      task2GreyZoneTrialsPerBlock:  int('task2GreyZoneTrialsPerBlock', 0),
      // Sum, recorded only. The asset derives the real total from the counts.
      task2TrialsPerBlock:
        int('task2ClearHitTrialsPerBlock', 0) +
        int('task2NearHitTrialsPerBlock', 0) +
        int('task2NearMissTrialsPerBlock', 0) +
        int('task2ClearMissTrialsPerBlock', 0) +
        int('task2GreyZoneTrialsPerBlock', 0),
      task2BreakDurationSeconds:  num('task2BreakDurationSeconds', 60),
      // Hit / near-miss / miss offsets removed from the form — categories are
      // shoulder-edge-anchored bands in the protocol asset, scaled by
      // shoulderWidthCm. setup.json values come from the asset.
      task2HitOffsetCm:           0,
      task2NearMissOffsetCm:      0,
      task2MissOffsetCm:          0,
      task2BallSpeeds:            ['slow', 'fast'], // label constant, recorded only
      // Ball velocities in m/s. 0 = keep the asset value.
      task2FastSpeed:             num('task2FastSpeed', 0),
      task2SlowSpeed:             num('task2SlowSpeed', 0),

      // Free-form
      clinicianNotes: fd.get('clinicianNotes') || '',
    };

    // Fresh session -> clear any live state left over from a previous task/run
    // so the Live view starts clean regardless of what came before.
    resetLiveState();

    const btn = $('#startBtn');
    const original = btn.textContent;
    btn.disabled = true;

    try {
      const r = await postJson('/api/session/start', { metadata });
      if (!r.ok) {
        $('#startError').textContent = r.error || 'Start failed. Check that the headset is connected.';
        return;
      }
      // Switch to live view automatically.
      $$('.tab').find(b => b.dataset.tab === 'live').click();
    } catch (err) {
      $('#startError').textContent = err.message || 'Request failed.';
    } finally {
      btn.disabled = false;
      btn.textContent = original;
    }
  });

  // ---- Live controls ----
  // After every command, immediately re-fetch /api/status so the UI updates
  // even if the WebSocket push is delayed or down.
  $('#pauseBtn').addEventListener('click',  async () => {
    log('cmd', 'pause →', new Date().toISOString());
    const r = await postJson('/api/session/pause');
    log('cmd', `pause ${r.ok ? 'ok' : 'fail: ' + (r.error || '')}`, new Date().toISOString());
    await refreshStatus();
  });
  $('#resumeBtn').addEventListener('click', async () => {
    log('cmd', 'resume →', new Date().toISOString());
    const r = await postJson('/api/session/resume');
    log('cmd', `resume ${r.ok ? 'ok' : 'fail: ' + (r.error || '')}`, new Date().toISOString());
    await refreshStatus();
  });
  $('#stopBtn').addEventListener('click', async () => {
    if (!confirm('End the session? This will write the final logs and return the headset to setup.')) return;
    log('cmd', 'stop →', new Date().toISOString());
    const r = await postJson('/api/session/stop');
    log('cmd', `stop ${r.ok ? 'ok' : 'fail: ' + (r.error || '')}`, new Date().toISOString());
    await refreshStatus();
  });

  // Belt-and-braces: poll status every 2s so the UI stays current even if
  // the WebSocket dies or never connects. Cheap GET.
  setInterval(refreshStatus, 2000);
  async function refreshStatus() {
    try { const s = await getJson('/api/status'); applyStatus(s); }
    catch { /* server gone — leave the disconnected pill */ }
  }

  // ---- Sessions ----
  $('#refreshSessions').addEventListener('click', refreshSessions);

  async function refreshSessions() {
    const tbody = $('#sessionsTable tbody');
    tbody.innerHTML = '<tr><td colspan="5" class="muted">loading…</td></tr>';
    try {
      const r = await getJson('/api/sessions');
      $('#protoVer').textContent = r.protocolVersion || '?';
      const rows = (r.sessions || []).map(s => `
        <tr>
          <td>${esc(s.participantId || '—')}</td>
          <td>${esc(s.sessionDate || '—')}</td>
          <td>${esc(s.sessionFolder)}</td>
          <td>${s.trialCount}</td>
          <td>
            <a href="/api/sessions/${encodeURIComponent(s.sessionFolder)}/metadata" target="_blank">metadata</a>
            <a href="/api/sessions/${encodeURIComponent(s.sessionFolder)}/trials" download="${s.sessionFolder}_trials.csv">trials.csv</a>
            <a href="/api/sessions/${encodeURIComponent(s.sessionFolder)}/eyetracking" download="${s.sessionFolder}_eye.csv">eye.csv</a>
            ${s.hasProgressSnapshot ? `<a href="/api/sessions/${encodeURIComponent(s.sessionFolder)}/progress" target="_blank">progress</a>` : ''}
          </td>
        </tr>`).join('');
      tbody.innerHTML = rows || '<tr><td colspan="5" class="muted">No sessions yet.</td></tr>';
    } catch (err) {
      // "Failed to fetch" = the Unity server isn't reachable. The Sessions
      // library is served BY the headset app, so it is only browsable while
      // Unity is in Play mode.
      const offline = !state.connected || /failed to fetch/i.test(err.message || '');
      tbody.innerHTML = offline
        ? '<tr><td colspan="5" class="muted">Headset offline — the session library is served by the Unity app. Press Play in Unity, wait for "connected", then Refresh.</td></tr>'
        : `<tr><td colspan="5" class="error">${esc(err.message)}</td></tr>`;
    }
  }

  // ---- Wire up status + WS ----
  initStatus();
  connectWs();

  async function initStatus() {
    try {
      const s = await getJson('/api/status');
      applyStatus(s);
    } catch (e) { /* server might not be ready yet */ }
  }

  function applyStatus(s) {
    state.status = s;
    $('#protoVer').textContent = s.protocolVersion || '?';

    const phase = s.phase || 'Idle';
    const pill = $('#phasePill');
    pill.textContent = phase;
    pill.classList.remove('running', 'paused');
    if (s.isPaused) pill.classList.add('paused');
    else if (s.isRunning) pill.classList.add('running');

    $('#participantPill').textContent = s.participantId ? `(${s.participantId})` : '';

    $('#liveSphase').textContent = phase + (s.isPaused ? ' (paused)' : '');
    $('#liveBlock').textContent  = `block ${s.currentBlockIndex >= 0 ? s.currentBlockIndex + 1 : '—'}`;
    $('#liveTrials').textContent = `${s.trialsCompletedInBlock || 0} / ${s.totalTrialsInBlock || 0}`;

    // Hint state with opacity but never *disable* the controls — the server
    // returns 409 if the command is invalid, and we'd rather let the user
    // click than have the UI block them based on stale status.
    $('#pauseBtn').style.opacity  = (s.isRunning && !s.isPaused) ? '1' : '0.55';
    $('#resumeBtn').style.opacity = (s.isRunning && s.isPaused)  ? '1' : '0.55';
    $('#stopBtn').style.opacity   = s.isRunning ? '1' : '0.55';
    $('#pauseBtn').disabled = false;
    $('#resumeBtn').disabled = false;
    $('#stopBtn').disabled = false;

    // REC pill: shown only when the TaskLogger is open (past practice).
    const rec = $('#recPill');
    if (rec) rec.hidden = !s.isRecording;

    applyDesignCells(s);
    applyTaskKind(s.taskKind);
  }

  // Reacts to which Unity scene is hosting the server. taskKind is the string
  // form of the C# enum ("Task1Pps" / "Task2HitOrMiss"). Drives:
  //   - the header task badge
  //   - which task-parameter fieldset is shown / highlighted
  //   - the Start button label
  //   - a full live-state reset whenever the scene *changes* (Task 1 -> Task 2)
  function applyTaskKind(taskKind) {
    const task1 = $('#task1Fieldset');
    const task2 = $('#task2Fieldset');

    // Detect a genuine scene change and wipe live state so Task 2 never
    // inherits Task 1's accuracy counters, event log, or last-trial readout.
    // Only fires on a real transition between two known kinds, so a transient
    // undefined during reconnect doesn't clear anything.
    if (taskKind && state.currentTaskKind && taskKind !== state.currentTaskKind) {
      resetLiveState();
      log('scene', `switched to ${TASK_META[taskKind]?.label || taskKind}`, new Date().toISOString());
    }
    if (taskKind) state.currentTaskKind = taskKind;

    // Header badge
    const badge = $('#taskBadge');
    const badgeLabel = $('#taskBadgeLabel');
    if (badge && badgeLabel) {
      badge.classList.remove('is-task1', 'is-task2');
      const meta = TASK_META[taskKind];
      if (meta) {
        badge.classList.add(meta.badge);
        badgeLabel.textContent = meta.label;
      } else {
        badgeLabel.textContent = 'No task loaded';
      }
    }

    // Start button label
    const startBtn = $('#startBtn');
    if (startBtn) startBtn.textContent = TASK_META[taskKind]?.start || 'Start session';

    if (!task1 || !task2) return;

    // Show + highlight the active task's fieldset; hide the other. If taskKind
    // is unknown (older server / mid-reconnect), keep both visible.
    if (taskKind === 'Task1Pps') {
      task1.style.display = '';  task1.classList.add('is-active');
      task2.style.display = 'none'; task2.classList.remove('is-active');
    } else if (taskKind === 'Task2HitOrMiss') {
      task1.style.display = 'none'; task1.classList.remove('is-active');
      task2.style.display = '';  task2.classList.add('is-active');
    } else {
      task1.style.display = ''; task1.classList.remove('is-active');
      task2.style.display = ''; task2.classList.remove('is-active');
    }
  }

  // Clears all per-run live UI + counters. Called on scene switch and on
  // starting a new session.
  function resetLiveState() {
    state.correctCount = 0;
    state.totalScored = 0;
    $('#liveAccuracy').textContent = 'accuracy —';
    $('#liveLast').textContent = '—';
    const ul = $('#eventLog');
    if (ul) ul.innerHTML = '';
  }

  function connectWs() {
    const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${proto}//${location.host}/ws`;

    try { state.socket?.close(); } catch (_) {}
    const sock = new WebSocket(url);
    state.socket = sock;

    sock.addEventListener('open', () => {
      console.log('[clinician] WS connected', url);
      setConn(true);
      state.reconnectMs = WS_RECONNECT_MIN_MS;
    });

    sock.addEventListener('close', (e) => {
      console.warn('[clinician] WS closed', e.code, e.reason);
      setConn(false);
      // Aggressive at the start (250 ms, 375 ms, 562 ms ...), capped at 3 s.
      // Designed for the "open browser right after pressing Play" case and for
      // the Task 1 -> Task 2 scene swap, where the server briefly goes down.
      state.reconnectMs = Math.min(state.reconnectMs * 1.5, WS_RECONNECT_MAX_MS);
      setTimeout(connectWs, state.reconnectMs);
    });

    sock.addEventListener('error', (e) => {
      console.error('[clinician] WS error', e);
      try { sock.close(); } catch (_) {}
    });

    sock.addEventListener('message', (e) => {
      let env;
      try { env = JSON.parse(e.data); }
      catch { return; }
      let payload = {};
      try { payload = env.payload ? JSON.parse(env.payload) : {}; }
      catch { /* leave empty */ }
      handleEvent(env.type, payload, env.serverTime);
    });
  }

  function setConn(ok) {
    state.connected = ok;
    const pill = $('#connStatus');
    pill.textContent = ok ? 'connected' : 'disconnected';
    pill.className = 'conn-pill ' + (ok ? 'conn-connected' : 'conn-disconnected');

    const banner = $('#waitingBanner');
    if (banner) banner.hidden = ok;
  }

  function handleEvent(type, p, ts) {
    switch (type) {
      case 'server_status':
        applyStatus(p);
        break;
      case 'phase_changed':
        log(type, `→ ${p.phase} (block ${p.currentBlockIndex + 1})`, ts);
        break;
      case 'trial_started':
        log(type, `${p.trialId} ${p.category} @ ${p.speedMps?.toFixed?.(1)}m/s${p.isSwitchTrial ? '  ⇄' : ''}`, ts);
        break;
      case 'trial_completed': {
        if (p.result === 'Correct')   { state.correctCount++; state.totalScored++; }
        else if (p.result === 'Incorrect') { state.totalScored++; }
        const acc = state.totalScored ? (100 * state.correctCount / state.totalScored).toFixed(1) : '—';
        $('#liveAccuracy').textContent = `accuracy ${acc}% (${state.correctCount}/${state.totalScored})`;
        $('#liveLast').textContent =
          `${p.trialId}  ${p.category}  ${p.received}  ${p.result}  RT ${formatRt(p.reactionTimeMs)}`;
        log(type, `${p.trialId} ${p.received} → ${p.result} (RT ${formatRt(p.reactionTimeMs)})`, ts, p.result?.toLowerCase());
        break;
      }
      case 'session_started':
        // A fresh run started on the headset — clear counters so this run's
        // accuracy starts from zero even if the panel was left open.
        resetLiveState();
        log(type, p.note || '', ts);
        break;
      case 'session_paused':
      case 'session_resumed':
      case 'session_ended':
        log(type, p.note || '', ts);
        break;
      case 'recording_started':
        log(type, '— CSV recording started', ts, 'recording');
        break;
    }
  }

  // ---- Helpers ----

  function log(type, msg, ts, klass = '') {
    const ul = $('#eventLog');
    const li = document.createElement('li');
    li.className = `ev-${type} ${klass}`;
    const t = ts ? new Date(ts) : new Date();
    li.innerHTML = `<span class="ts">${pad(t.getHours())}:${pad(t.getMinutes())}:${pad(t.getSeconds())}</span><b>${esc(type)}</b> ${esc(msg)}`;
    ul.prepend(li);
    while (ul.children.length > 200) ul.removeChild(ul.lastChild);
  }

  function pad(n) { return n < 10 ? '0' + n : '' + n; }
  function esc(s) { return String(s ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch])); }
  function formatRt(ms) { return (ms === undefined || ms === null || isNaN(ms) || ms < 0) ? '—' : `${ms.toFixed(0)} ms`; }

  async function getJson(url) {
    const r = await fetch(url);
    if (!r.ok) throw new Error(`HTTP ${r.status}`);
    return r.json();
  }
  async function postJson(url, body = {}) {
    const r = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    return r.json().catch(() => ({ ok: r.ok }));
  }

})();
