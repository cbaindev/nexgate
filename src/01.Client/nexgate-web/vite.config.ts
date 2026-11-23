/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

// La SPA y la API se sirven en el mismo origen (design de add-admin-panel-shell, decisión 4): en desarrollo, Vite hace de
// proxy de /api hacia la API (NEXGATE_API, por defecto http://localhost:5095), así la cookie de renovación viaja sin CORS.
const api = process.env.NEXGATE_API ?? 'http://localhost:5095'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'prompt',
      injectRegister: false,
      includeAssets: ['favicon.svg', 'theme-init.js', 'icons/apple-touch-icon.png'],
      manifest: {
        name: 'Nexgate',
        short_name: 'Nexgate',
        description: 'Control de acceso de edificios y barrios',
        lang: 'es',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        background_color: '#f9f9fc',
        theme_color: '#3f35c8',
        icons: [
          { src: 'icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: 'icons/icon-512.png', sizes: '512x512', type: 'image/png' },
          { src: 'icons/icon-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // Solo los archivos del build (decisión 7): sin reglas en tiempo de ejecución, ninguna respuesta de la API se
        // guarda en el dispositivo.
        // Incluye la fuente Inter (woff2): la PWA se ve igual sin conexión (design de refine-admin-panel-design, decisión 2).
        // Solo los subconjuntos latinos: los demás (cirílico, griego, vietnamita) se descargan si hacen falta.
        globPatterns: ['**/*.{js,css,html,svg,png,webmanifest,woff2}'],
        globIgnores: ['**/inter-{cyrillic,cyrillic-ext,greek,greek-ext,vietnamese}-*.woff2'],
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [],
      },
    }),
  ],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: api, changeOrigin: false },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    restoreMocks: true,
    // Una zona lejos de la de los sitios: las pantallas deben usar la hora del sitio, no la del navegador.
    env: { TZ: 'Asia/Tokyo' },
    // Pruebas de integración de pantallas completas: con todas en paralelo, 5 s por prueba se queda corto.
    testTimeout: 15_000,
  },
})
