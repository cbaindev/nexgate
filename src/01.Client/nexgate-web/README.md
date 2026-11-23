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
- **Pantallas** en `src/features/<sección>`, con sus consultas de TanStack Query. Las claves empiezan por `['org', organizationId]`.
- **Componentes** de shadcn/ui en `src/components/ui` (sobre Radix, con Tailwind).
- **Textos** en `src/i18n/es.json`: ninguna pantalla escribe textos sueltos, y una prueba falla si falta una clave.
- **PWA** (vite-plugin-pwa):
  - El service worker precarga solo los archivos del build, sin reglas de caché para `/api`. `scripts/check-pwa.mjs` lo verifica sobre el build.
  - Sin red, la aplicación abre y avisa que no hay conexión.

## Reglas

- Nada de `localStorage`, `sessionStorage` ni `document.cookie` para datos de sesión (lo impide el lint).
- La SPA y la API se sirven en el **mismo origen** (la cookie no viaja con CORS). En producción, el servidor estático o el proxy inverso enruta `/api` hacia la API.
- Una pantalla nueva: se agrega a `sections.ts`, se registra en `App.tsx`, sus textos van a `es.json` y sus pruebas usan la API simulada de `src/test/fakeApi.ts`.
