// Aplica el tema antes del primer pintado, sin destello (design de refine-admin-panel-design, decisión 3). Se carga
// bloqueante en el <head> desde el mismo origen (sin script en línea). Lee la misma clave que src/theme/theme.ts.
;(function () {
  var preference = 'system'
  try {
    var stored = localStorage.getItem('nexgate.theme')
    if (stored === 'light' || stored === 'dark') preference = stored
  } catch (e) {
    // Almacenamiento bloqueado: se sigue al sistema.
  }
  var dark = preference === 'dark' || (preference === 'system' && !!window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)
  var root = document.documentElement
  if (dark) root.classList.add('dark')
  root.style.colorScheme = dark ? 'dark' : 'light'
  var meta = document.querySelector('meta[name="theme-color"]')
  if (meta) meta.setAttribute('content', dark ? '#161a33' : '#3f35c8')
})()
