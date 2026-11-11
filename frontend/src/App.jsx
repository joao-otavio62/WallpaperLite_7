import { useEffect, useState } from 'react';
import { call, onEvent, isDemo } from './bridge';
import './styles.css';

export default function App() {
  const [st, setSt] = useState({ items: [], activeId: null, paused: false, autoPaused: false });
  const [url, setUrl] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    call('getState').then(setSt).catch((e) => setError(e.message));
    return onEvent((m) => m.event === 'state' && setSt(m.data));
  }, []);

  const run = async (fn) => {
    setBusy(true);
    setError('');
    try { await fn(); } catch (e) { setError(e.message); } finally { setBusy(false); }
  };

  const importUrl = (e) => {
    e.preventDefault();
    if (url.trim()) run(async () => { await call('importUrl', { url: url.trim() }); setUrl(''); });
  };

  const active = st.items.find((i) => i.id === st.activeId);
  const state = !active ? 'Nenhum wallpaper ativo'
    : st.autoPaused ? `Pausado automaticamente (${st.autoReason})`
    : st.paused ? 'Pausado' : 'Em exibição';

  return (
    <main>
      {isDemo && (
        <p className="demo" role="status">
          Modo demonstração: esta página está aberta no navegador e não altera o wallpaper do PC.
          Abra o programa com o run.bat.
        </p>
      )}
      <header className="now">
        <div>
          <p className={`state ${active && !st.paused && !st.autoPaused ? 'on' : ''}`}>{state}</p>
          <h1>{active ? active.name : 'Escolha um wallpaper'}</h1>
        </div>
        {active && (
          <div className="actions">
            {active.kind === 'gif' && (
              <button onClick={() => run(() => call('togglePause'))}>{st.paused ? 'Retomar' : 'Pausar'}</button>
            )}
            <button onClick={() => run(() => call('stop'))}>Restaurar o original</button>
          </div>
        )}
      </header>

      <section className="import">
        <form onSubmit={importUrl}>
          <input
            type="url" value={url} onChange={(e) => setUrl(e.target.value)}
            placeholder="https://exemplo.com/imagem.gif" aria-label="Link da imagem (https)" disabled={busy}
          />
          <button className="primary" disabled={busy || !url.trim()}>{busy ? 'Baixando…' : 'Importar link'}</button>
        </form>
        <button onClick={() => run(() => call('importLocal'))} disabled={busy}>Escolher arquivo</button>
      </section>
      <section className="desktop-settings" aria-label="Configurações da área de trabalho">
        <h2>Área de trabalho</h2>
        <div className="actions">
          <button onClick={() => run(() => call('toggleDesktopIcons'))} disabled={busy}>
            {st.desktopIconsVisible === false ? 'Mostrar ícones da área de trabalho' : 'Ocultar ícones da área de trabalho'}
          </button>
          <button onClick={() => run(() => call('toggleTaskbar'))} disabled={busy}>
            {st.taskbarVisible === false ? 'Mostrar barra de tarefas' : 'Ocultar barra de tarefas'}
          </button>
        </div>
        <p className="hint">Ao ocultar, o WallpaperLite libera a tela usando a ocultação automática do Windows; mova o mouse até a borda inferior para exibir a barra. O estado original é restaurado ao sair. Use Ctrl+Alt+T para alternar a barra.</p>
      </section>
      <p className="hint">Aceita GIF, PNG e JPG de até 20 MB. Links precisam começar com https://.</p>
      {error && <p className="error" role="alert">{error}</p>}

      {st.items.length === 0 ? (
        <p className="empty">Sua biblioteca está vazia. Cole um link ou escolha um arquivo do computador.</p>
      ) : (
        <ul className="grid">
          {st.items.map((it) => (
            <li key={it.id} className={it.id === st.activeId ? 'current' : ''}>
              <div className="thumb">
                {it.thumb && <img src={it.thumb} alt="" loading="lazy" />}
                <span className="kind">{it.kind}</span>
              </div>
              <p className="name" title={it.name}>{it.name}</p>
              <div className="row">
                <button className="primary" disabled={busy || it.id === st.activeId}
                  onClick={() => run(() => call('apply', { id: it.id }))}>
                  {it.id === st.activeId ? 'Em uso' : 'Aplicar'}
                </button>
                <button onClick={() => run(() => call('remove', { id: it.id }))} disabled={busy}>Excluir</button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
