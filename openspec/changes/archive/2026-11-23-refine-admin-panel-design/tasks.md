# Tasks

## 1. Fundamentos del sistema de diseño

- [x] 1.1 Tokens índigo en claro y oscuro (estado, barra lateral, sombras y radios) en `src/index.css`, variante `dark` por clase y prueba de contraste AA sobre los pares de tokens; verificar con `npm run check`
- [x] 1.2 Inter variable empaquetada (`@fontsource-variable/inter`) y escala tipográfica; verificar en el build que el `woff2` está en la precarga y que no hay pedidos a otros orígenes
- [x] 1.3 Tema claro, oscuro o del sistema (`src/theme/theme.ts`, `public/theme-init.js` sin destello, excepción del lint acotada, `theme-color`); verificar con pruebas de los escenarios de `Color theme`
- [x] 1.4 Movimiento con CSS y anulación global con `prefers-reduced-motion`; verificar en el navegador que la regla de movimiento reducido está en los estilos cargados

## 2. Componentes

- [x] 2.1 Ajustar `button`, `card`, `alert` y `form`; agregar `badge`, `skeleton`, `avatar`, `sheet`, `separator`, `empty-state` y `page-header`; verificar con `npm run check`
- [x] 2.2 Página `/_design` solo en desarrollo con la paleta y los componentes; verificar que el build de producción no la incluye y revisarla en los dos temas

## 3. Marca y marco

- [x] 3.1 Logo (isotipo y logotipo), favicon, íconos de la PWA generados con `scripts/icons.mjs` y manifiesto con los colores de marca; verificar con `scripts/check-pwa.mjs` (manifiesto, íconos y precarga) y revisando los íconos generados
- [x] 3.2 Barra lateral fija con grupos (`group` en `sections.ts`), encabezado con título y menú de usuario (avatar, organización, tema y salida) y menú inferior en celular; verificar con las pruebas de navegación por rol y en el navegador en escritorio y celular

## 4. Pantallas

- [x] 4.1 Inicio de sesión con panel de marca y elección de organización; verificar con las pruebas de sesión
- [x] 4.2 Inicio con accesos rápidos y páginas de mensaje con `empty-state`; verificar con las pruebas de navegación
- [x] 4.3 Sitios con `page-header`, tarjetas con ícono e insignia, formulario en `sheet`, esqueletos y estado vacío; verificar con pruebas de los escenarios de `Loading and empty states` y las de `Sites screen`

## 5. Verificación y documentación

- [x] 5.1 Revisión en el navegador con la API real: inicio, sitios, panel lateral, menú de la cuenta, inicio de sesión y `/_design`, en claro y oscuro, escritorio y celular, con foco por teclado
- [x] 5.2 README de la SPA (sistema de diseño, tema, cómo agregar componentes), `docs/05` (frontend) y `docs/08` (7c implementado); verificar releyendo contra los specs
- [x] 5.3 Verificación final: `npm run check` en verde, y `dotnet build` y `dotnet test` en verde (sin cambios en el backend)
