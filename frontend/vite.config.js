import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// CSP só no build (o dev server do Vite injeta scripts inline).
const csp = {
  name: 'csp',
  apply: 'build',
  transformIndexHtml: (html) =>
    html.replace(
      '<head>',
      `<head>\n    <meta http-equiv="Content-Security-Policy" content="default-src 'self'; img-src 'self' https://thumbs.local data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'none'">`
    ),
};

export default defineConfig({
  base: './',
  plugins: [react(), csp],
  server: { port: 5173, strictPort: true },
  build: { outDir: 'dist', sourcemap: false },
});
