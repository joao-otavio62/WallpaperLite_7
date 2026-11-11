# WallpaperLite

Gerenciador de wallpapers (estáticos e GIF animados) com **frontend e backend separados**.

```
WallpaperLite/
├── frontend/                      React + Vite (somente UI)
│   └── src/bridge.js              contrato de mensagens com o C# (com mock p/ navegador)
└── backend/WallpaperLite.Host/    C# .NET 8 + WPF + WebView2 (motor nativo)
    ├── WebBridge.cs               único ponto de entrada do front; valida tudo
    ├── Native.cs                  P/Invoke: SystemParametersInfo, WorkerW, shell hook
    ├── WallpaperManager.cs        biblioteca, aplicar/parar, pausa
    ├── AnimatedWallpaperWindow.cs GIF no WorkerW, frames sob demanda (SkiaSharp)
    ├── PauseMonitor.cs            monitor de pausa automática (desativado)
    ├── SafeDownloader.cs          https-only, anti-SSRF, limite 20 MB, nome GUID
    ├── FileSignature.cs           magic numbers (GIF/PNG/JPG)
    ├── TrayIcon.cs / MainWindow.cs  modo bandeja; WebView2 só existe com a janela aberta
    └── app.manifest               asInvoker (sem admin)
```

## Uso rápido (Windows)
Dê dois cliques em **run.bat**. Abre uma janela do programa (não é um site) e um ícone na bandeja. A página no navegador é só demonstração e não altera o wallpaper.

## Rodar em desenvolvimento
```bash
cd frontend && npm install && npm run dev         # http://localhost:5173
dotnet run --project backend/WallpaperLite.Host   # Debug carrega o Vite dev server
```
Só a UI (sem C#): abra http://localhost:5173 no navegador; `bridge.js` usa dados de demonstração.

## Gerar pacote para compartilhar (Windows x64)
No computador de desenvolvimento, com Node.js/npm e o .NET 8 SDK instalados, execute `publish.bat`. Ele gera o frontend, publica uma versão autocontida e cria `release/WallpaperLite-win-x64.zip`.

Envie esse ZIP. Seus amigos devem extrair a pasta `WallpaperLite-win-x64` inteira e executar `Iniciar WallpaperLite.bat`. Eles não precisam instalar Node.js nem .NET. O Microsoft Edge WebView2 Runtime é necessário; ele já costuma estar instalado no Windows 11. Se estiver ausente, o arquivo `LEIA-ME.txt` no pacote aponta para o instalador oficial.

## Publicação manual
```bash
cd frontend && npm ci && npm run build
dotnet publish backend/WallpaperLite.Host -c Release -r win-x64 --self-contained true -p:UseAppHost=true -o release/WallpaperLite-win-x64
```
O `frontend/dist` é copiado para `wwwroot/` ao lado do .exe e servido em `https://app.local` (virtual host do WebView2, sem servidor HTTP). A publicação inclui o runtime do .NET, mas requer o WebView2 Runtime no computador de destino.

## Protocolo da ponte
| `type` | `payload` | efeito |
|---|---|---|
| `getState` | – | devolve `{ items, activeId, paused, autoPaused, clockEnabled, desktopIconsVisible, taskbarVisible }` |
| `importUrl` | `{ url }` | baixa com segurança e adiciona à biblioteca |
| `importLocal` | – | abre o seletor nativo (GIF/PNG/JPG) |
| `apply` / `remove` | `{ id }` | aplica / exclui |
| `stop` / `togglePause` | – | restaura o original / pausa manual |
| `toggleDesktopIcons` / `toggleTaskbar` | – | mostra ou oculta os ícones da área de trabalho / barra de tarefas; ao ocultar, ativa temporariamente o modo de ocultação automática do Windows para liberar a tela. O estado original é restaurado ao sair. Ctrl+Alt+T também alterna a barra de tarefas |
| `setClockEnabled` | `{ enabled }` | mostra ou oculta um relógio digital centralizado no topo do wallpaper; a preferência é salva |

O backend também emite `{ event: "state", data }` sempre que algo muda (inclusive a pausa automática).

Na seção **Área de trabalho**, ative **Mostrar relógio sobre o wallpaper** para exibir a hora local em formato 24 horas, com segundos, centralizada horizontalmente e a 20 px do topo do monitor principal. O relógio é transparente a cliques e não fica sobre janelas de outros aplicativos.

Em versões recentes do Windows 11 (build 26100 ou superior), o modo automático do GIF usa uma janela de topo porque os encaixes tradicionais em `Progman`/`WorkerW` podem não aparecer. Esse modo pode cobrir os ícones da área de trabalho; os outros encaixes continuam disponíveis no menu da bandeja.

## Mapa do documento de requisitos
- **Memória**: SKCodec decodifica um frame por vez num único buffer; GC workstation não concorrente; sem Electron; WebView2 descartado ao fechar a janela.
- **Pausa**: apenas manual; a pausa automática por bateria, tela cheia ou janela maximizada está desativada.
- **Segurança**: magic numbers, nome de arquivo GUID, só `https://`, bloqueio de IPs internos no `ConnectCallback` (também vale para redirecionamentos, que são seguidos manualmente, máx. 3), HEAD + limite rígido de 20 MB durante o stream, manifesto `asInvoker`, WebView2 restrito a `app.local` + CSP no build, mensagens de erro sem detalhes internos.

## Próximos passos sugeridos
Iniciar com o Windows (`--tray` já é suportado), multi-monitor, ícone próprio na bandeja.
