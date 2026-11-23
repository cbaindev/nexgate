# Tasks

## 1. Identity: cookie de renovación y operador en `/me`

- [x] 1.1 `refreshTokenInCookie` en el inicio de sesión, cookie `nexgate_refresh` y lectura de la cookie en renovación y cierre de sesión; verificar con pruebas de integración de los escenarios nuevos de `authentication` (atributos de la cookie incluidos)
- [x] 1.2 `platformOperator` en `GET /me`; verificar con la prueba de usuario actual
- [x] 1.3 Regenerar `api/nexgate-v1.openapi.json` y revisar el diff; verificar con `dotnet build` sin warnings y `dotnet test` en verde

## 2. Base de la SPA

- [x] 2.1 Crear `src/01.Client/nexgate-web` con Vite, React, TypeScript estricto, Tailwind, shadcn/ui, ESLint, Vitest, Testing Library y MSW; proxy de `/api` en desarrollo; verificar con `npm run check` y `npm run dev` contra la API
- [x] 2.2 i18n con el catálogo en español y la prueba de claves faltantes; verificar con `npm run check`
- [x] 2.3 Cliente generado: `api:generate`, `api:check`, `src/api/schema.d.ts` y cliente openapi-fetch; verificar que `api:check` falla con un contrato modificado y pasa con el versionado

## 3. Sesión y organización

- [x] 3.1 Módulo de sesión (token en memoria, restauración con la cookie, renovación programada y ante 401, cierre de sesión) y traductor de errores; verificar con pruebas unitarias
- [x] 3.2 Pantallas de inicio de sesión y de elección de organización, cambio desde el menú y vaciado de la caché; verificar con pruebas de componentes de los escenarios de `Sign-in and session` y `Active organization`

## 4. Navegación, PWA y sitios

- [x] 4.1 Tabla de secciones, diseño con barra lateral y menú en celular, `RequireSection` y páginas de "sin permiso" y "llega en un próximo cambio"; verificar con pruebas de navegación por rol
- [x] 4.2 PWA: manifiesto, íconos, service worker sin caché de la API, aviso sin conexión y de actualización; verificar con el build (manifiesto y service worker generados, sin rutas `/api` en la precarga) y en el navegador
- [x] 4.3 Pantalla de sitios (listado, alta, edición, solo lectura, errores por campo); verificar con pruebas de componentes de `Sites screen`

## 5. Verificación y documentación

- [x] 5.1 Verificación manual en el navegador con la API real: inicio de sesión, recarga, cambio de organización, alta de un sitio, instalación y apertura sin red
- [x] 5.2 README de la SPA y del repositorio (cómo levantarla y verificarla), `docs/05` (frontend y sesión), `docs/08` (cambio 7 implementado y `add-admin-panel-screens` agregado); verificar releyendo contra los specs
- [x] 5.3 Verificación final: `dotnet build` sin warnings, `dotnet test` y `npm run check` en verde

## Notas de implementación

- **Renovaciones serializadas.** La verificación en el navegador encontró que dos renovaciones simultáneas (React monta dos veces en desarrollo, y también pasa con dos pestañas) mandan el mismo token de renovación. El servidor lo toma como reutilización y revoca la sesión. Ahora las renovaciones van de a una: en la pestaña con una cola, y entre pestañas con Web Locks. La restauración al arrancar se hace una sola vez. Hay una prueba que lo verifica.
- **Reintentos.** Las consultas se reintentan solo sin conexión: un 500 se muestra enseguida con su código de referencia, y un 401 ya lo reintenta el cliente después de renovar.
- **Cookie con la sesión.** La cookie vence con la sesión de renovación: `TokenPair` suma `SessionExpiresAt`. Una renovación con cookie que falla con 401 también borra la cookie.
- **Versiones.** Las dependencias quedaron en versiones fijas probadas (Vite 6, React 19.1, React Router 7, TypeScript 5.8, Vitest 3, ESLint 9). Subir a las majors más nuevas queda para otro momento.
- **Verificación de la PWA.** `npm run check` suma `scripts/check-pwa.mjs` sobre el build: manifiesto instalable, íconos y ninguna ruta de la API en la precarga. En el navegador, con el servidor detenido, la aplicación abrió desde el service worker y mostró "Sin conexión" sin datos.
- **Verificación manual con la API real.**
  - Hecho: inicio de sesión, recarga sin pedir credenciales, cookie ilegible para JavaScript y nada en `localStorage` ni `sessionStorage`, alta de un sitio, vista en celular y apertura sin red.
  - Pendiente: el cambio de organización solo quedó probado con la API simulada, porque el usuario de desarrollo tiene una sola organización.
- **Resultado final.** Build sin warnings. Todas las pruebas en verde: 46 de arquitectura, 406 de la API y 38 de la SPA.

