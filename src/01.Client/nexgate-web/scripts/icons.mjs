// Genera el isotipo de Nexgate y los íconos de la PWA (design de refine-admin-panel-design, decisión 8).
// Uso: `npm run icons`. Los archivos generados se versionan; correrlo solo al cambiar el logo.
//
// Isotipo: una puerta entreabierta sobre un cuadrado índigo, con dos arcos cian de "señal" (el acceso inteligente).
import { mkdir, writeFile } from 'node:fs/promises'
import sharp from 'sharp'

const indigo = { from: '#5b4ff0', to: '#3a2db8' }
const cyan = '#3fd5ec'

/** Dibujo de la puerta en un lienzo de 64×64. */
const glyph = `
  <path d="M18 51V17a4 4 0 0 1 4-4h16a4 4 0 0 1 4 4v34" fill="none" stroke="#fff" stroke-width="3.6" stroke-linejoin="round"/>
  <path d="M13 51.5h38" stroke="#fff" stroke-width="3.6" stroke-linecap="round"/>
  <path d="M22 15.6 36 19.6v29.2L22 51.4Z" fill="#fff"/>
  <circle cx="32.2" cy="35.6" r="2.2" fill="${indigo.to}"/>
  <path d="M46.5 10.2a7.5 7.5 0 0 1 5.3 5.3" fill="none" stroke="${cyan}" stroke-width="3" stroke-linecap="round"/>
  <path d="M47.6 4.2a13.5 13.5 0 0 1 10.2 10.2" fill="none" stroke="${cyan}" stroke-width="3" stroke-linecap="round" opacity=".7"/>`

/**
 * @param {object} options
 * @param {number} options.radius radio de las esquinas del fondo (0 = cuadrado completo, para íconos enmascarables)
 * @param {number} options.scale escala del dibujo (menor para dejar la zona segura de los íconos enmascarables)
 */
function mark({ radius = 14, scale = 1 } = {}) {
  const offset = (64 - 64 * scale) / 2
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="${indigo.from}"/>
      <stop offset="1" stop-color="${indigo.to}"/>
    </linearGradient>
  </defs>
  <rect width="64" height="64" rx="${radius}" fill="url(#g)"/>
  <g transform="translate(${offset} ${offset}) scale(${scale})">${glyph}
  </g>
</svg>
`
}

const publicDir = new URL('../public/', import.meta.url)
await mkdir(new URL('icons/', publicDir), { recursive: true })
await mkdir(new URL('brand/', publicDir), { recursive: true })

const rounded = mark()
await writeFile(new URL('favicon.svg', publicDir), rounded)
await writeFile(new URL('brand/nexgate-mark.svg', publicDir), rounded)

const png = (svg, size, file) => sharp(Buffer.from(svg), { density: 72 * (size / 64) * 2 }).resize(size, size).png().toFile(new URL(file, publicDir).pathname.replace(/^\/(\w:)/, '$1'))

await png(rounded, 192, 'icons/icon-192.png')
await png(rounded, 512, 'icons/icon-512.png')
// Enmascarable: fondo completo y el dibujo dentro de la zona segura (80 % central).
await png(mark({ radius: 0, scale: 0.72 }), 512, 'icons/icon-maskable-512.png')
// iOS redondea las esquinas por su cuenta.
await png(mark({ radius: 0, scale: 0.86 }), 180, 'icons/apple-touch-icon.png')

console.log('Íconos generados en public/.')
