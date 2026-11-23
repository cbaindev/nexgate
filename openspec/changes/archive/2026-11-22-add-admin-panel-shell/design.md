# Design

## Context

La API REST está completa para el prototipo (Tenancy, Identity, Spaces, Access, Devices y Events), con contrato OpenAPI versionado (D-34) en `api/nexgate-v1.openapi.json`. Identity emite un token de acceso JWT de 15 minutos y un token de renovación opaco que rota en cada uso, ambos en el cuerpo de la respuesta. `GET /me` devuelve la cuenta y sus organizaciones; `GET /me/permissions` devuelve los permisos efectivos de la organización activa. `docs/05` fija React + TypeScript, Vite, TanStack Query y React Router, y deja abierta la biblioteca de componentes. Ver `proposal.md` para el alcance y las tres decisiones tomadas con el usuario: **shadcn/ui con Tailwind**, **token de renovación en cookie httpOnly** y **openapi-typescript con openapi-fetch**.

## Goals / Non-Goals

**Goals**
- Una base de SPA sobre la que cada cambio siguiente solo agregue pantallas.
- Sesión segura sin tokens accesibles a scripts persistidos en el navegador.
- El contrato como única fuente de tipos: si la API cambia, la SPA no compila hasta regenerarlos.

**Non-Goals**
- Pantallas de negocio más allá de Sitios, vistas de guardia o residente, tiempo real y funcionamiento sin conexión con datos.

## Decisions

### 1. Proyecto y herramientas

`src/01.Client/nexgate-web`, un proyecto npm independiente (no forma parte de `Nexgate.slnx`):

- Vite + React 19 + TypeScript estricto, React Router (modo datos) y TanStack Query.
- Tailwind CSS y componentes de shadcn/ui copiados en `src/components/ui` (Radix por debajo, accesibles). Íconos con lucide-react.
- i18next con un catálogo `src/i18n/es.json`; ningún texto de interfaz escrito en los componentes.
- Vitest + Testing Library + MSW (servidor simulado de la API con los tipos generados) para las pruebas; ESLint y `tsc --noEmit`.
- `npm run check` = tipos, lint, pruebas, sincronía con el contrato y build. Es la verificación de la SPA en las tareas, al lado de `dotnet build` y `dotnet test`.
- Versiones fijadas en `package.json` y `package-lock.json` versionado.
- **Alternativa descartada:** un proyecto `.esproj` dentro de la solución. Ata la SPA a Visual Studio y al ciclo de `dotnet build` sin beneficio; la SPA solo depende del contrato, no del código .NET.

### 2. Cliente generado del contrato

- `npm run api:generate` corre openapi-typescript sobre `../../../api/nexgate-v1.openapi.json` y escribe `src/api/schema.d.ts` (versionado).
- `npm run api:check` regenera en memoria y compara con el archivo; si difieren, falla con el comando para regenerar. Así un cambio de contrato rompe la verificación de la SPA, igual que la prueba de contrato rompe `dotnet test`.
- `src/api/client.ts` crea un cliente openapi-fetch con base `/api` y un middleware que agrega el token de acceso y, ante un 401, renueva la sesión una vez y reintenta.
- Las consultas y mutaciones de TanStack Query se escriben a mano por pantalla (`src/features/<seccion>/queries.ts`), con claves que empiezan por la organización activa.
- **Alternativa descartada:** Orval (decisión del usuario). Genera hooks por operación: mucho código generado para pocas pantallas, y más configuración para el manejo de sesión.

### 3. Token de renovación en cookie (cambio en Identity)

- `POST /auth/token` suma `refreshTokenInCookie` (booleano, opcional). Con `true`, la respuesta pone el token de renovación en la cookie y el campo `refreshToken` del cuerpo queda nulo.
- Cookie `nexgate_refresh`: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/v1/auth`, vence con la sesión de renovación (`Identity:Tokens:RefreshSessionDays`).
- `POST /auth/refresh` y `POST /auth/logout`: `refreshToken` del cuerpo pasa a ser opcional. Si no viene, se usa la cookie. Una renovación hecha con la cookie la reemplaza y devuelve el cuerpo sin token de renovación. El cierre de sesión borra la cookie. Sin token en el cuerpo ni cookie: 401 `invalid_refresh_token` (el código que ya usa una renovación inválida).
- Las operaciones solo aceptan `application/json` (ya es así): un formulario de otro sitio no puede mandar JSON sin preflight, y `SameSite=Strict` impide que la cookie viaje desde otro sitio. No hace falta un token anti-CSRF aparte.
- El token de acceso queda en memoria (un módulo de sesión, fuera de React). Al recargar, la SPA llama a `POST /auth/refresh` con la cookie para obtener uno nuevo.
- `Secure` en `localhost` funciona en los navegadores actuales (localhost cuenta como contexto seguro), así que el desarrollo por HTTP con el proxy de Vite no necesita excepciones.
- Los clientes que hoy usan el cuerpo (pruebas, herramientas) no cambian: el modo cookie es opcional.
- **Alternativa descartada:** `localStorage`. Un XSS podría robar el token de renovación y mantener la sesión indefinidamente.
- **Alternativa descartada:** solo en memoria. Cada recarga o cada apertura de la PWA pide credenciales.

### 4. Mismo origen

La SPA y la API se sirven en el mismo origen, así la cookie viaja sin CORS. En desarrollo, Vite (puerto 5173) hace de proxy de `/api` hacia `http://localhost:5095`. En producción, el servidor estático o el proxy inverso enruta `/api` a la API (se define con el hosting).

- **Alternativa descartada:** CORS con credenciales. Obliga a `SameSite=None` y a listas de orígenes, más superficie para el mismo resultado.

### 5. Sesión, organización activa y caché

- Estados de la sesión: sin iniciar → restaurando (renovación con la cookie al arrancar) → sin organización (varias membresías) → activa.
- El cambio de organización es `POST /auth/refresh` con `organizationId`. Después se vacía la caché entera de TanStack Query: nunca se mezclan datos de dos organizaciones.
- La renovación se programa 60 segundos antes del vencimiento (`expiresIn`), y además ante un 401. Si la renovación falla con 401, se vuelve al inicio de sesión. Si falla con 403 (la membresía terminó), se pide elegir otra organización.
- `GET /me` incluye `platformOperator`. Hoy el operador solo se conoce leyendo la base en cada solicitud; exponerlo en `/me` no agrega datos sensibles y evita adivinar con un 403.

### 6. Navegación por permisos

- Una tabla única `src/app/sections.ts`: ruta, clave de texto, ícono y requisito (permiso con cualquier alcance, u operador de plataforma).
- La barra lateral (o el menú inferior en celular) y las rutas se construyen desde esa tabla. Un `RequireSection` en cada ruta muestra "sin permiso" sin montar la pantalla, así no se pide ningún dato.
- Secciones: Inicio (todos), Sitios (`sites.read`), Espacios y puertas (`spaces.read`), Accesos (`access.read`), Dispositivos (`devices.read`), Eventos (`events.read`), Miembros (`members.read`) y Plataforma (operador). Las que no tienen pantalla muestran "llega en un próximo cambio".

### 7. PWA

- `vite-plugin-pwa` con estrategia `generateSW`: precarga solo los archivos del build y `navigateFallback` a `index.html`. Sin reglas de caché en tiempo de ejecución para `/api`, así ninguna respuesta de la API se guarda.
- Manifiesto: nombre "Nexgate", `display: standalone`, íconos de 192 y 512 px (y versión enmascarable), color de tema.
- Sin red, la aplicación abre el esqueleto. La restauración de la sesión falla por red y se muestra el aviso de "sin conexión" con un botón para reintentar; no se muestran datos guardados.
- Actualización: cuando hay una versión nueva del service worker, se avisa con un botón para recargar.

### 8. Errores

Un traductor único de problem details:

| Respuesta | Presentación |
|---|---|
| 400 con `errors` | mensaje del servidor junto a cada campo (las claves coinciden con los nombres de los campos) |
| 401 | renovación y reintento; si falla, inicio de sesión |
| 403 | "No tenés permiso para esta operación" |
| 404 | "No encontramos lo que buscabas" |
| 409 | el título del servidor (ya viene en español) |
| 429 | "Demasiados intentos. Probá de nuevo en un minuto." |
| 500 | mensaje genérico con `correlationId` |
| sin red | "Sin conexión" con reintentar |

### 9. Pantalla de sitios

Listado con nombre, zona horaria y estado. Formulario de alta y edición con nombre y zona horaria (selector con las zonas IANA de Argentina primero). Las acciones se muestran solo con `sites.manage` en ese sitio o en la organización. Valida en el cliente solo lo obvio (obligatorio); el resto lo dice el servidor.

## Cómo se prueba

- **Backend (dotnet test):**
  - Integración del modo cookie: inicio de sesión sin token en el cuerpo y con la cookie con sus atributos; renovación y cierre de sesión con la cookie; renovación sin token ni cookie.
  - `GET /me` con `platformOperator`.
  - Contrato regenerado.
- **SPA (npm run check):**
  - Unitarias del módulo de sesión (programación de la renovación, reintento único ante 401) y del traductor de errores.
  - Componentes con MSW: inicio de sesión (válido, inválido, 429), elección de organización, navegación por rol (administrador, guardia, residente, operador), "sin permiso" por dirección, pantalla de sitios (listado, alta, error por campo, solo lectura).
  - Ningún acceso a `localStorage` o `sessionStorage` con tokens.
  - Catálogo de textos completo y sincronía con el contrato.
- **Manual en el navegador** con la API real: iniciar sesión, recargar, cambiar de organización, crear un sitio, instalar la PWA y abrirla sin red.

## Risks / Trade-offs

- **Dos ecosistemas** (.NET y Node): más herramientas para el desarrollador. Mitigación: `npm run check` es un solo comando, documentado en el README.
- **Cookie y mismo origen:** el hosting de producción tiene que enrutar `/api` en el mismo dominio. Se documenta como requisito del despliegue.
- **shadcn/ui copia componentes** al repositorio: hay que mantenerlos a mano. A cambio, no hay dependencia de una biblioteca de estilos que fije el diseño.
- **Pantallas pendientes:** el panel mínimo de la fase 1 necesita un cambio más (`add-admin-panel-screens`), que se agrega a `docs/08`.

## Migration Plan

Sin migraciones de base. El cambio de Identity es compatible: los clientes que mandan el token en el cuerpo siguen igual.

## Open Questions

- Dominio y hosting de la SPA en producción (con `/api` en el mismo origen).
