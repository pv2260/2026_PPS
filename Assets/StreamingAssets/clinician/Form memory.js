// Percep PD — Clinician Panel: form memory.
//
// Keeps the New Session form filled in across reloads. The panel is served
// from the headset, so a Unity restart, a scene switch, or a plain refresh
// used to wipe everything the clinician had typed and the subject details had
// to be entered again.
//
// Everything is stored in the browser's localStorage, on the laptop running
// the panel. Nothing is sent anywhere and nothing here touches the session
// payload, so the data that reaches Unity is exactly what is visible in the
// form at the moment Start is pressed.
//
// Load AFTER app.js so the restore runs on top of app.js's own prefills:
//   <script src="app.js"></script>
//   <script src="form-memory.js"></script>

(() => {
  const STORAGE_KEY = 'percepPd.sessionFormDraft.v1';

  // Fields that should never be restored from an old draft.
  // sessionDate is re-prefilled to today by app.js on every load.
  const SKIP_FIELDS = new Set(['sessionDate']);

  const form = document.querySelector('#sessionForm');
  if (!form) return;

  // ---- Read / write ----

  function collect() {
    const data = {};
    form.querySelectorAll('[name]').forEach((el) => {
      if (!el.name || SKIP_FIELDS.has(el.name)) return;
      data[el.name] = el.type === 'checkbox' ? el.checked : el.value;
    });
    return data;
  }

  function save() {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({
        savedAt: new Date().toISOString(),
        fields: collect(),
      }));
    } catch (err) {
      console.warn('[form-memory] could not save draft', err);
    }
  }

  function load() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch (err) {
      console.warn('[form-memory] could not read draft', err);
      return null;
    }
  }

  function clear() {
    try { localStorage.removeItem(STORAGE_KEY); } catch (err) { /* ignore */ }
  }

  // ---- Restore ----

  function restore(draft) {
    if (!draft || !draft.fields) return 0;

    let restored = 0;

    Object.keys(draft.fields).forEach((name) => {
      if (SKIP_FIELDS.has(name)) return;

      const el = form.querySelector(`[name="${name}"]`);
      if (!el) return;

      const value = draft.fields[name];

      if (el.type === 'checkbox') {
        el.checked = !!value;
      } else {
        el.value = value;
      }

      restored++;

      // app.js binds trial totals and the design-cell warning to 'input',
      // so fire both events to keep the derived UI in sync.
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
    });

    return restored;
  }

  // ---- Restored-draft notice ----

  function showNotice(draft) {
    const when = draft.savedAt ? new Date(draft.savedAt) : null;

    const stamp = when
      ? when.toLocaleString(undefined, {
          day: '2-digit', month: '2-digit',
          hour: '2-digit', minute: '2-digit',
        })
      : 'earlier';

    const bar = document.createElement('div');
    bar.id = 'formMemoryNotice';
    bar.style.cssText = [
      'display:flex',
      'align-items:center',
      'gap:12px',
      'margin:12px 0',
      'padding:10px 14px',
      'border:1px solid rgba(120,120,120,.35)',
      'border-radius:8px',
      'font-size:.9rem',
    ].join(';');

    const text = document.createElement('span');
    text.textContent = 'Restored the form as last edited on ' + stamp + '.';
    text.style.flex = '1';

    const clearBtn = document.createElement('button');
    clearBtn.type = 'button';
    clearBtn.textContent = 'Clear and start blank';
    clearBtn.addEventListener('click', () => {
      clear();
      location.reload();
    });

    const dismissBtn = document.createElement('button');
    dismissBtn.type = 'button';
    dismissBtn.textContent = 'Dismiss';
    dismissBtn.addEventListener('click', () => bar.remove());

    bar.append(text, clearBtn, dismissBtn);
    form.parentElement.insertBefore(bar, form);
  }

  // ---- Wiring ----

  let saveTimer = null;

  function scheduleSave() {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(save, 250);
  }

  form.addEventListener('input', scheduleSave);
  form.addEventListener('change', scheduleSave);

  // Catch anything typed in the last moments before a reload or a Unity restart.
  window.addEventListener('beforeunload', save);
  window.addEventListener('pagehide', save);

  // Restore once the DOM and app.js prefills have settled.
  function boot() {
    const draft = load();
    if (!draft) return;

    const count = restore(draft);
    if (count > 0) {
      showNotice(draft);
      console.log('[form-memory] restored ' + count + ' fields from draft.');
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => setTimeout(boot, 0));
  } else {
    setTimeout(boot, 0);
  }
})();