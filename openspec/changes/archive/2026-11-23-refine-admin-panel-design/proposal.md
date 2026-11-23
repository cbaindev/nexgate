# Proposal

## Why

La base de la SPA (`add-admin-panel-shell`) funciona, pero se ve como un esqueleto: colores y tipografía por defecto, una tarjeta suelta para iniciar sesión, textos de "Cargando…" y listados planos. El usuario pidió que se vea **más moderna y profesional, con mejor diseño visual y colores**. Conviene hacerlo antes de `add-admin-panel-screens` (7b), para que las pantallas nuevas nazcan con el diseño final y no haya que retocarlas después. Es el cambio 7c de `docs/08`.

## What Changes

- **Identidad visual** (decisiones del usuario):
  - Paleta de marca **azul índigo** con acentos cian, neutros fríos y colores de estado (éxito, advertencia, peligro, información).
  - Tipografía **Inter** (variable), servida desde el propio build, sin CDN externo, para que la PWA funcione sin conexión.
  - Logo nuevo (isotipo y logotipo) y, con él, favicon, íconos de la PWA, `theme-color` y color de fondo del manifiesto.
- **Sistema de diseño** sobre los tokens de shadcn/ui: escala de espaciados, radios, sombras y elevaciones, estados de foco visibles, variantes de botón, insignias, avisos con ícono y campos de formulario con estado de error. Una página de muestra solo en desarrollo para revisar los componentes en claro y oscuro.
- **Modo oscuro con selector**: sigue al sistema por defecto y el menú de usuario permite elegir claro, oscuro o sistema. La preferencia se recuerda en el dispositivo (no es dato de sesión) y se aplica antes del primer pintado, sin destello.
- **Marco de la aplicación**:
  - **Barra lateral fija** (decisión del usuario) con logo, organización activa y secciones agrupadas, con la sección activa resaltada. En celular sigue el menú inferior, rediseñado.
  - Encabezado con título de la sección, avatar con iniciales y menú de usuario con organización, tema y cierre de sesión.
- **Pantallas existentes rediseñadas**: inicio de sesión con panel de marca, elección de organización, inicio con accesos rápidos a las secciones permitidas, sitios con tarjetas de mejor jerarquía (tipo con ícono e insignia, zona horaria) y formulario en un panel lateral, y las páginas de "sin permiso", "no encontrada", "llega en un próximo cambio" y "sin conexión".
- **Estados de carga y vacíos**: esqueletos de carga en lugar de textos, estados vacíos ilustrados con la acción principal cuando el usuario puede realizarla.
- **Microanimaciones discretas** (aparición de paneles y menús, transición de la navegación) que se desactivan si el sistema pide reducir el movimiento.
- **Accesibilidad**: contraste WCAG AA en claro y oscuro, foco visible en todo control y navegación por teclado.

Fuera de alcance:
- Pantallas nuevas de negocio (son de `add-admin-panel-screens`).
- Barra lateral plegable, navegación superior y personalización de colores por organización (marca blanca).
- Gráficos y tableros con métricas.
- Pruebas de regresión visual automatizadas (capturas comparadas). La revisión visual es manual en el navegador.
- Cambios en la API o en el contrato OpenAPI.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `admin-shell`: identidad visual, tema claro, oscuro o del sistema elegido por el usuario, estados de carga y vacíos, movimiento reducido y accesibilidad visual.

## Impact

- Código modificado: solo `src/01.Client/nexgate-web` (estilos, componentes de `src/components/ui`, marco, pantallas, íconos de `public/`, catálogo `es.json` y pruebas). Dependencias nuevas: la fuente Inter empaquetada y los componentes de Radix que hagan falta (por ejemplo el panel lateral y el avatar), con versiones fijas.
- Backend, contrato OpenAPI y base de datos: sin cambios.
- **Multitenancy:** sin cambios. El nombre de la organización activa se muestra igual que hoy, desde la sesión.
- **Seguridad:** la única información que se guarda en el navegador es la preferencia de tema (`light`, `dark` o `system`), en un único módulo con una excepción explícita del lint; tokens y datos de sesión siguen prohibidos en el almacenamiento del navegador. La fuente se sirve desde el mismo origen: ningún recurso externo.
- **Privacidad:** sin datos personales nuevos en pantalla. El avatar usa iniciales del correo, que ya se muestra; no se cargan fotos ni servicios externos (como Gravatar).
- Decisiones relacionadas: D-29 (implementa: SPA progresiva) y D-05 (navegación por rol, sin cambios de comportamiento).
