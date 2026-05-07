// Clinician console — vanilla JS SPA. No build step.
//
// Talks to the Unity headset through:
//   - REST  /api/...   (commands + reads)
//   - WS    /ws        (live event stream)
//
// All event payloads use the WsEnvelope { type, payload, serverTime } format
// from NetworkMessages.cs. The payload string is JSON — parse on demand.

(() => {
  const $  = (sel, root = document) => root.querySelector(sel);
  const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

  const state = {
    connected: false,
    status: null,
    correctCount: 0,
    totalScored: 0,
    socket: null,
    reconnectMs: 1000,
  };

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

      // Task 1 parameters
      task1NumberOfBlocks:        int('task1NumberOfBlocks', 4),
      task1TrialsPerBlock:        int('task1TrialsPerBlock', 40),
      task1BreakDurationSeconds:  num('task1BreakDurationSeconds', 30),
      task1NarrowOffsetCm:        num('task1NarrowOffsetCm', 5),
      task1WideOffsetCm:          num('task1WideOffsetCm', 15),
      task1LoomingSpeeds:         csv('task1LoomingSpeeds'),
      task1PracticeVtOnlyTrials:  int('task1PracticeVtOnlyTrials', 2),
      task1PracticeVtVisualTrials:int('task1PracticeVtVisualTrials', 4),

      // Task 2 parameters
      task2NumberOfBlocks:        int('task2NumberOfBlocks', 4),
      task2TrialsPerBlock:        int('task2TrialsPerBlock', 40),
      task2BreakDurationSeconds:  num('task2BreakDurationSeconds', 30),
      task2HitOffsetCm:           num('task2HitOffsetCm', 0),
      task2NearMissOffsetCm:      num('task2NearMissOffsetCm', 5),
      task2MissOffsetCm:          num('task2MissOffsetCm', 15),
      task2BallSpeeds:            csv('task2BallSpeeds'),

      // Free-form
      clinicianNotes: fd.get('clinicianNotes') || '',
    };

    try {
      const r = await postJson('/api/session/start', { metadata });
      if (!r.ok) {
        $('#startError').textContent = r.error || 'failed';
        return;
      }
      // Switch to live view automatically.
      $$('.tab').find(b => b.dataset.tab === 'live').click();
    } catch (err) {
      $('#startError').textContent = err.message || 'request failed';
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
      tbody.innerHTML = rows || '<tr><td colspan="5" class="muted">no sessions yet</td></tr>';
    } catch (err) {
      tbody.innerHTML = `<tr><td colspan="5" class="error">${esc(err.message)}</td></tr>`;
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
      state.reconnectMs = 1000;
    });

    sock.addEventListener('close', (e) => {
      console.warn('[clinician] WS closed', e.code, e.reason);
      setConn(false);
      // Exponential-ish backoff so a downed server doesn't flood the console.
      state.reconnectMs = Math.min(state.reconnectMs * 1.5, 8000);
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
      case 'trial_completed':
        if (p.result === 'Correct')   { state.correctCount++; state.totalScored++; }
        else if (p.result === 'Incorrect') { state.totalScored++; }
        const acc = state.totalScored ? (100 * state.correctCount / state.totalScored).toFixed(1) : '—';
        $('#liveAccuracy').textContent = `accuracy ${acc}% (${state.correctCount}/${state.totalScored})`;
        $('#liveLast').textContent =
          `${p.trialId}  ${p.category}  ${p.received}  ${p.result}  RT ${formatRt(p.reactionTimeMs)}`;
        log(type, `${p.trialId} ${p.received} → ${p.result} (RT ${formatRt(p.reactionTimeMs)})`, ts, p.result?.toLowerCase());
        break;
      case 'session_paused':
      case 'session_resumed':
      case 'session_started':
      case 'session_ended':
        log(type, p.note || '', ts);
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
  function formatRt(ms) { return (ms === undefined || ms === null || isNaN(ms)) ? '—' : `${ms.toFixed(0)} ms`; }

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
