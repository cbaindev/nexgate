# nexgate-web

SPA progresiva (PWA) de Nexgate (D-29): una sola aplicación web, instalable, con vistas según los permisos del usuario (D-05), que consume solo la API REST a través de tipos generados del contrato (D-34). Diseño en `openspec/specs/admin-shell` y en el cambio `add-admin-panel-shell`.

## Comandos

| Comando | Qué hace |
|---|---|
| `npm install` | Instala las dependencias (versiones fijas, `package-lock.json` versionado) |
| `npm run dev` | Servidor de desarrollo en `http://localhost:5173`, con proxy de `/api` hacia la API (`NEXGATE_API`, por defecto `http://localhost:5095`) |
| `npm run check` | Verificación completa: tipos de la API al día, TypeScript, lint, pruebas, build y PWA |
| `npm run api:generate` | Regenera `src/api/schema.d.ts` desde `api/nexgate-v1.openapi.json` |
| `npm test` | Pruebas (Vitest, Testing Library y una API simulada con MSW) |
| `npm run build` y `npm run preview` | Build de producción y servidor local del build (con el service worker) |
| `npm run icons` | Regenera el isotipo y los íconos de la PWA desde `scripts/icons.mjs` |

## Cómo está armada

- **Cliente de la API** (`src/api`):
  - `schema.d.ts` se genera del contrato con openapi-typescript y no se edita a mano; `npm run api:check` falla si no coincide con el contrato.
  - `client.ts` expone `api` (operaciones de negocio con el token, que ante un 401 renueva y reintenta una vez) y `authApi` (autenticación, con la cookie).
- **Sesión** (`src/session/session.ts`):
  - El token de acceso vive solo en memoria.
  - El de renovación viaja en la cookie `nexgate_refresh`: `HttpOnly`, `SameSite=Strict`, acotada a `/api/v1/auth`. JavaScript nunca la lee.
  - Al cargar, la sesión se restaura renovando con esa cookie. La renovación se programa un minuto antes del vencimiento.
  - Las renovaciones van de a una, también entre pestañas (Web Locks): el servidor revoca la sesión si ve dos veces el mismo token de renovación.
  - Cambiar de organización es renovar con `organizationId` y vacía toda la caché de datos.
- **Navegación** (`src/app/sections.ts`): una tabla única de secciones con el permiso que exige cada una. De ahí salen el menú lateral, el menú inferior en celular y las rutas. Una sección sin permiso muestra "sin permiso" sin pedir datos. Es solo presentación: el servidor verifica cada operación.
- **Pantallas** en `src/features/<sección>`, con sus consultas de TanStack Query. Las claves empiezan por `['org', organizationId]`; las de un sitio, por `['org', organizationId, 'sites', siteId]`, así invalidar el sitio refresca todo lo suyo.
- **Secciones por sitio** (cambio `add-spaces-screens`):
  - `src/components/site-picker` tiene `useSelectedSite()`, `SitePicker` (el desplegable del encabezado) y `SiteGate` (carga, error, sin sitios, elegir uno o sitio no disponible).
  - El sitio elegido va en la dirección (`?sitio=`) y se elige solo cuando hay uno. Lo que se elige dentro del sitio también va en la dirección: en espacios, `?espacio=` y `?vista=puertas`.
- **Permisos con alcance:**
  - `hasPermissionOnSite` y `hasPermissionOnSpace` en `session.ts` deciden qué acciones se muestran.
  - Un permiso con alcance de un espacio alcanza a ese espacio y a todo lo que tiene adentro; las puertas se manejan con permiso sobre el espacio al que dan.
  - Es solo presentación: el servidor verifica cada operación.
- **Sistema de diseño** (cambio `refine-admin-panel-design`):
  - **Colores:** tokens en `src/index.css` (`--primary`, `--muted`, `--success`, `--sidebar`, …) para claro y oscuro. Las pantallas usan las clases de los tokens (`bg-primary`, `text-muted-foreground`), nunca la paleta de Tailwind directa (`indigo-600`). `src/test/contrast.test.ts` verifica el contraste WCAG AA de los pares de tokens: si cambiás un color, corré las pruebas.
  - **Tipografía:** Inter variable (`@fontsource-variable/inter`), servida desde el build. El service worker precarga los subconjuntos latinos.
  - **Componentes** de shadcn/ui adaptados en `src/components/ui`:
    - `button` (con `loading`), `card`, `alert` (con variantes y su ícono), `form`, `badge` y `dropdown-menu`.
    - `sheet` (panel lateral para formularios), `skeleton`, `avatar` (iniciales), `empty-state` (con ilustración), `page-header` y `separator`.
    - `tabs`, `table` y `confirm-dialog`, la confirmación de lo que no se puede deshacer, que deja el mensaje del servidor en el diálogo si rechaza.
    - Un componente nuevo se copia de shadcn/ui y se adapta a los tokens.
  - **Muestra:** `http://localhost:5173/_design`, solo en desarrollo: la paleta y los componentes en el tema activo. El build de producción no la incluye (`scripts/check-pwa.mjs` lo verifica).
  - **Movimiento:** animaciones con `@keyframes` propias (`animate-enter`, `animate-sheet-in`, `animate-rise`, …). Con `prefers-reduced-motion` se anulan todas.
  - **Logo:** el isotipo y los íconos de la PWA salen de `scripts/icons.mjs` (`npm run icons`). Los archivos generados se versionan.
- **Tema** (`src/theme/theme.ts`):
  - Claro, oscuro o del sistema (por defecto), elegido desde el menú de la cuenta o desde el botón de las pantallas sin sesión.
  - La preferencia se guarda con la clave `nexgate.theme`. Es lo único que la aplicación guarda en el navegador, y por eso es el único archivo con excepción a la regla del lint.
  - `public/theme-init.js`, bloqueante en el `<head>`, aplica la clase `dark` antes del primer pintado, sin destello. Es un archivo y no un script en línea, así una CSP no necesita `unsafe-inline`.
- **Textos** en `src/i18n/es.json`: ninguna pantalla escribe textos sueltos, y una prueba falla si falta una clave.
- **PWA** (vite-plugin-pwa):
  - El service worker precarga solo los archivos del build, sin reglas de caché para `/api`. `scripts/check-pwa.mjs` lo verifica sobre el build.
  - Sin red, la aplicación abre y avisa que no hay conexión.

## Reglas

- Nada de `localStorage`, `sessionStorage` ni `document.cookie` para datos de sesión (lo impide el lint). La única excepción es la preferencia de tema, en `src/theme/theme.ts`.
- La SPA y la API se sirven en el **mismo origen** (la cookie no viaja con CORS). En producción, el servidor estático o el proxy inverso enruta `/api` hacia la API.
- Una pantalla nueva:
  - Se agrega a `sections.ts` con su grupo y su descripción, y se registra en `App.tsx`.
  - Empieza con `PageHeader`, muestra esqueletos al cargar y un `EmptyState` sin datos, y sus formularios van en un `Sheet`.
  - Si trabaja dentro de un sitio, usa `useSelectedSite`, `SitePicker` y `SiteGate`.
  - Eliminar pasa por `ConfirmDialog`.
  - Sus textos van a `es.json` y sus pruebas usan la API simulada de `src/test/fakeApi.ts`.
