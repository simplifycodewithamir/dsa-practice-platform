import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // `vite preview` serves the production build, which is what the Playwright suite runs against;
  // it needs the same /api proxy the dev server has.
  preview: {
    proxy: {
      '/api': { target: 'http://localhost:8080', changeOrigin: true },
    },
  },
  server: {
    // Dev talks to the Api through this proxy, so the browser sees one origin and CORS never
    // enters into it locally. In production the two are on different hosts, which is why the Api
    // still configures CORS explicitly.
    proxy: {
      '/api': { target: 'http://localhost:8080', changeOrigin: true },
    },
  },
  test: {
    // The Playwright specs live in e2e/ and use @playwright/test, which Vitest cannot run.
    exclude: ['e2e/**', 'node_modules/**', 'dist/**'],
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: true,
  },
});
