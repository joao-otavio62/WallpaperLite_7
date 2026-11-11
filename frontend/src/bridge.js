// Ponte React <-> C#. Contrato: { id, type, payload } -> { id, ok, data | error }
// Eventos do backend: { event: 'state', data }
const wv = window.chrome?.webview;
export const isDemo = !wv;
const pending = new Map();
const listeners = new Set();
let seq = 0;

wv?.addEventListener('message', ({ data: m }) => {
  if (m.event) return listeners.forEach((fn) => fn(m));
  const p = pending.get(m.id);
  if (!p) return;
  pending.delete(m.id);
  m.ok ? p.resolve(m.data) : p.reject(new Error(m.error));
});

export const onEvent = (fn) => (listeners.add(fn), () => listeners.delete(fn));

export function call(type, payload) {
  if (!wv) return mock(type, payload); // navegador comum (sem backend)
  return new Promise((resolve, reject) => {
    const id = ++seq;
    pending.set(id, { resolve, reject });
    wv.postMessage({ id, type, payload });
  });
}

// ---- mock para desenvolver a UI sem o C# ----
const demo = {
  items: [], activeId: null, paused: false, autoPaused: false,
  desktopIconsVisible: true, taskbarVisible: true,
};
async function mock(type, p) {
  const emit = () => listeners.forEach((fn) => fn({ event: 'state', data: structuredClone(demo) }));
  if (type === 'importUrl' || type === 'importLocal') {
    demo.items.unshift({ id: String(Date.now()), name: p?.url ? new URL(p.url).host : 'arquivo-local.gif', kind: 'gif', thumb: '' });
  }
  if (type === 'apply') demo.activeId = p.id;
  if (type === 'remove') demo.items = demo.items.filter((i) => i.id !== p.id);
  if (type === 'stop') demo.activeId = null;
  if (type === 'togglePause') demo.paused = !demo.paused;
  if (type === 'toggleDesktopIcons') demo.desktopIconsVisible = !demo.desktopIconsVisible;
  if (type === 'toggleTaskbar') demo.taskbarVisible = !demo.taskbarVisible;
  if (type !== 'getState') emit();
  return structuredClone(demo);
}
