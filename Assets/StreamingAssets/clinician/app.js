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
  // Trials per block is computed from the three explicit PPS trial counts:
  // VT + visual-only + tactile-only. The total field is read-only in the form.
  bindTask1TrialCountTotal();

  function bindTask1TrialCountTotal() {
    const form = $('#sessionForm');
    if (!form) return;

    const vt = form.querySelector('[name="task1VtTrialsPerBlock"]');
    const v  = form.querySelector('[name="task1VisualOnlyTrialsPerBlock"]');
    const t  = form.querySelector('[name="task1TactileOnlyTrialsPerBlock"]');
    // Live total now lives in a caption span, not a readonly input — only
    // genuinely modifiable fields remain as inputs.
    const total = $('#task1TrialTotal');

    if (!vt || !v || !t || !total) return;

    const toInt = (el) => {
      const value = parseInt(el.value, 10);
      return isNaN(value) ? 0 : Math.max(0, value);
    };

    const update = () => {
      total.textContent = toInt(vt) + toInt(v) + toInt(t);
    };

    [vt, v, t].forEach(el => el.addEventListener('input', update));
    update();
  }

  // ---- Start session ----
  $('#sessionForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    $('#startError').textContent = '';

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
      // IMPORTANT: 0, not the displayed 72. A non-zero value here feeds the
      // legacy ApplyTask2SessionOverrides path that splits a flat total
      // across categories, which would fight the settled 9/24/24/9 + 6 grey
      // per-category design in the protocol asset. 0 = asset decides.
      task2TrialsPerBlock:        0,
      task2BreakDurationSeconds:  num('task2BreakDurationSeconds', 60),
      // Hit / near-miss / miss offsets removed from the form — categories are
      // shoulder-edge-anchored bands in the protocol asset, scaled by
      // shoulderWidthCm. setup.json values come from the asset.
      task2HitOffsetCm:           0,
      task2NearMissOffsetCm:      0,
      task2MissOffsetCm:          0,
      task2BallSpeeds:            ['slow', 'fast'], // protocol constant, recorded only

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
