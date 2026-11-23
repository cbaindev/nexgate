# Proposal

## Why

El backend del prototipo técnico está completo (cambios 1 a 6), pero no hay ninguna interfaz: todo se prueba con Swagger. D-29 define que toda la aplicación web es una sola SPA progresiva, instalable, con vistas según rol (D-05), que consume solo la API REST a través de un cliente generado del contrato (D-34). Antes de construir pantallas de negocio hace falta la base común: el proyecto, la sesión, la elección de organización, la navegación según permisos, el cliente tipado y la instalación como PWA. Es el cambio 7 de `docs/08` y cierra la fase 1.

## What Changes

- **Proyecto de la SPA** en `src/01.Client/nexgate-web`: React + TypeScript con Vite, React Router y TanStack Query (`docs/05`), componentes shadcn/ui sobre Radix con Tailwind, interfaz en español con catálogo de i18n desde el inicio.
- **Cliente generado del contrato** (D-34): tipos generados con openapi-typescript desde `api/nexgate-v1.openapi.json` y un cliente fetch tipado (openapi-fetch). La verificación del proyecto falla si los tipos generados no coinciden con el contrato versionado.
- **Sesión**:
  - Inicio de sesión con correo y contraseña.
  - El token de acceso vive solo en memoria. El token de renovación viaja en una **cookie httpOnly** que JavaScript no puede leer, así la sesión sobrevive a una recarga y a reabrir la PWA sin exponer el token.
  - Renovación automática antes de vencer y ante un 401. Cierre de sesión que revoca la sesión en el servidor.
- **Organización activa**: con varias membresías, el usuario elige la organización al entrar y la puede cambiar desde el menú; con una sola, queda elegida.
- **Navegación por permisos** (D-05): las secciones (Sitios, Espacios y puertas, Accesos, Dispositivos, Eventos, Miembros y Plataforma) se muestran según `GET /me/permissions` y la marca de operador de plataforma. Entrar por URL a una sección sin permiso muestra una página de "sin permiso". Las secciones sin pantalla todavía muestran que llegan en un cambio próximo.
- **Primera pantalla de negocio: Sitios**, para probar el circuito completo con el cliente generado: listado, alta y modificación de sitios, con validación por campo desde los errores de la API.
- **PWA instalable**: manifiesto, íconos y service worker que guarda solo el esqueleto de la aplicación. Sin red, la aplicación abre y avisa que no hay conexión. Las respuestas de la API nunca se guardan en el dispositivo.
- **Presentación de errores**: errores por campo, falta de permiso, demasiados intentos, sin conexión y error inesperado con su identificador de correlación.
- **Cambios en Identity** (contrato de la API):
  - El inicio de sesión puede pedir el token de renovación en cookie. En ese modo, la renovación y el cierre de sesión lo leen de la cookie, y la respuesta ya no lo trae en el cuerpo.
  - `GET /me` informa si el usuario es operador de plataforma.

Fuera de alcance:
- Pantallas de espacios y puertas, accesos (sujetos, horarios, credenciales, otorgamientos), dispositivos, eventos y miembros. Se propone un cambio aparte, `add-admin-panel-screens`, para completar el "panel mínimo" de la fase 1 (`docs/07`).
- Vistas de guardia y de residente (`add-resident-pwa` y los cambios de cada flujo).
- Notificaciones push, SignalR y funcionamiento sin conexión con datos.
- Hosting de producción: CDN, cabeceras de seguridad del servidor estático y proxy inverso. En desarrollo, Vite hace de proxy de la API para servir todo en el mismo origen.
- Pruebas de punta a punta en navegador automatizadas (Playwright). En este cambio, la verificación con la API real es manual en el navegador.
- Cambio de contraseña, recuperación de cuenta y aceptación de invitaciones desde la SPA.

## Capabilities

### New Capabilities

- `admin-shell`: aplicación web progresiva, sesión, organización activa, navegación por permisos, cliente generado del contrato, presentación de errores y la pantalla de sitios.

### Modified Capabilities

- `authentication`: token de renovación en cookie httpOnly para la aplicación web; el usuario actual informa si es operador de plataforma.

## Impact

- Código nuevo: `src/01.Client/nexgate-web` (aplicación, pruebas con Vitest y Testing Library, tipos generados).
- Código modificado: Identity (modelos de solicitud y respuesta de autenticación, cookie de renovación y `GET /me`), contrato OpenAPI y su prueba. Ningún módulo nuevo en el backend.
- Herramientas: Node.js 22 o superior y npm para la SPA. `dotnet build` y `dotnet test` siguen cubriendo el backend; la SPA se verifica con `npm run check` (tipos, lint, pruebas, sincronía con el contrato y build).
- **Multitenancy:** la SPA nunca elige la organización por su cuenta: la cambia renovando el token con la organización elegida, y el servidor valida la membresía como siempre. Al cambiar de organización se descartan todos los datos en caché de la anterior.
- **Seguridad:**
  - El token de acceso solo vive en memoria.
  - El de renovación va en una cookie `HttpOnly`, `Secure`, `SameSite=Strict`, acotada a `/api/v1/auth`. Las operaciones que la leen solo aceptan JSON, así un formulario de otro sitio no puede usarla.
  - Nada sensible queda en `localStorage`, `sessionStorage` ni en la caché del service worker.
  - La navegación por permisos es solo de presentación: el servidor sigue verificando cada operación.
- **Privacidad:** la caché del service worker guarda solo archivos estáticos; ninguna respuesta de la API (con datos de personas) se guarda en el dispositivo. Los errores muestran el identificador de correlación, nunca datos internos.
- Decisiones relacionadas: D-29 y D-34 (implementa), D-05 (navegación por rol), D-33.
