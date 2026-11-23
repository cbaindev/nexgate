// Verifica el build de la PWA (design de add-admin-panel-shell, decisión 7): manifiesto instalable y service worker que
// solo precarga archivos del build, sin reglas de caché para la API.
import { readdir, readFile } from 'node:fs/promises'

const dist = new URL('../dist/', import.meta.url)
const fail = (message) => {
  console.error(`PWA: ${message}`)
  process.exit(1)
}

const manifest = JSON.parse(await readFile(new URL('manifest.webmanifest', dist), 'utf8'))
if (manifest.display !== 'standalone' || manifest.name !== 'Nexgate') fail('el manifiesto no es instalable (name, display).')
for (const size of ['192x192', '512x512']) {
  if (!manifest.icons?.some((icon) => icon.sizes === size)) fail(`falta el ícono de ${size}.`)
}
if (!manifest.icons.some((icon) => icon.purpose === 'maskable')) fail('falta el ícono enmascarable.')

const sw = await readFile(new URL('sw.js', dist), 'utf8')
const precache = [...sw.matchAll(/url:"([^"]+)"/g)].map((m) => m[1])
if (precache.length === 0) fail('el service worker no precarga archivos.')
if (precache.some((url) => url.startsWith('/api') || url.startsWith('api/'))) fail('el service worker precarga rutas de la API.')
if (/registerRoute\((?!.*NavigationRoute)/.test(sw)) fail('el service worker tiene reglas de caché en tiempo de ejecución.')

if (!precache.some((url) => /inter-latin-wght/.test(url))) fail('el service worker no precarga la fuente de la marca.')
if (!precache.includes('theme-init.js')) fail('el service worker no precarga theme-init.js.')

// La muestra del sistema de diseño (/_design) es solo de desarrollo.
for (const file of await readdir(new URL('assets/', dist))) {
  if (file.endsWith('.js') && (await readFile(new URL(`assets/${file}`, dist), 'utf8')).includes('design-invalid')) {
    fail('el build incluye la página /_design.')
  }
}

// Ningún recurso de otro origen en el build (fuentes, estilos o scripts de un CDN): la PWA se ve igual sin conexión.
const html = await readFile(new URL('index.html', dist), 'utf8')
if (/(src|href)="(https?:)?\/\//.test(html)) fail('index.html carga recursos de otro origen.')

console.log(`PWA: manifiesto instalable y ${precache.length} archivos precargados, ninguno de la API.`)
