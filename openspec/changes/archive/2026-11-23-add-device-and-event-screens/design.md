# Design

## Context

La SPA ya tiene el selector de sitio filtrado por permiso (`useSelectedSite({ permission })`), la hora del sitio (`src/lib/zoned-time.ts`), los datos de espacios y puertas compartidos en caché (`useSiteStructure`) y el patrón de lista y detalle de accesos.

La API de dispositivos ofrece:

| Operación | Endpoint | Detalle |
| --- | --- | --- |
| Listar y dar de alta | `GET/POST /sites/{siteId}/devices` | El alta devuelve `IssuedDevice`: dispositivo, `claimCode` y `claimExpiresAt`. |
| Leer, renombrar y eliminar | `GET/PUT/DELETE /devices/{id}` | |
| Asignar puertas | `PUT /devices/{id}/access-points` | Reemplaza el conjunto; responde 409 si otra controla una puerta. |
| Código nuevo | `POST /devices/{id}/claim-code` | Devuelve otro `IssuedDevice`; el dispositivo vuelve a pendiente. |
| Revocar | `POST /devices/{id}/revocation` | |

`DeviceDto` trae:
- `status`: `pending`, `active` o `revoked`.
- `accessPointIds`.
- `lastSeenAt`.
- `firmwareVersion`.
- `reportedPolicyVersion` y `policyVersion`.
- `certificateExpiresAt`.
- `claimExpiresAt`.

Los eventos salen de `GET /sites/{siteId}/access-events`:
- Filtros: `accessPointId`, `subjectId`, `from` y `to`; paginación con `cursor` y `limit` (hasta 100).
- Responde `{ items, nextCursor }`, del más reciente al más viejo por `occurredAt`.
- Cada evento trae `deviceId`, `accessPointId`, `decision`, `reason`, `credentialId`, `subjectId`, `occurredAt` y `receivedAt`.

`devices.*` y `events.read` valen solo con alcance de sitio u organización.

Decisiones del usuario:
- **Eventos que se actualizan solos cada 15 s.**
- **Código de reclamo en texto y QR.**

## Goals / Non-Goals

**Goals**
- Instalar un dispositivo de punta a punta desde la SPA: alta, código, puertas y verificación de que se conectó y tiene la política al día.
- Que un guardia vea lo que pasa en las puertas casi en vivo, sin tocar nada.
- Que el código de reclamo no quede en ningún lado.

**Non-Goals**
- WebSocket, alertas, exportación, OTA, diagnóstico y apertura remota.

## Decisions

### 1. Secciones y selector

- `/dispositivos` y `/eventos` usan `useSelectedSite({ permission: 'devices.read' | 'events.read' })` y `SiteGate` con los textos de "sin sitios disponibles", como accesos.
- **Dispositivos:**
  - Lista y detalle, con el elegido en `?dispositivo=<id>`; en celular el detalle va debajo.
  - Altas, renombres y asignaciones en `sheet`; revocar, código nuevo y eliminar con `confirm-dialog`.
- **Eventos:**
  - Una sola vista con filtros arriba y la tabla, que en celular se ve como tarjetas, con el mismo marcado `data-label` que las puertas.

### 2. Estado de un dispositivo

- **Insignia de estado:** Pendiente (info), Activo (éxito), Revocado (peligro).
- **Conexión**, solo si está activo:
  - "En línea" si `lastSeenAt` es de hace menos de 5 minutos;
  - "Sin contacto" si es más viejo;
  - "Nunca se conectó" si es nulo.
  - El umbral es una constante (`OnlineWindowMinutes = 5`). El intervalo de latido del firmware es del orden de un minuto, así que 5 minutos tolera latidos perdidos sin marcar un falso corte.
- **Último contacto en palabras** con `Intl.RelativeTimeFormat('es')` ("hace 3 minutos"), y la fecha exacta en la hora del sitio como `title`. Va en `src/lib/relative-time.ts`.
- **Política:**
  - "Al día" si `reportedPolicyVersion === policyVersion`.
  - "Actualización pendiente (v3 de v4)" si es menor.
  - "Todavía no tiene política" si `policyVersion` es nulo: no controla puertas o todavía no se reclamó.
- **Certificado:** vencimiento en la hora del sitio; aviso de advertencia si faltan menos de 30 días.
- **Pendiente:** "Esperando que se reclame. El código vence el …", con `claimExpiresAt`.
- El detalle se refresca cada 30 s mientras está a la vista, así el último contacto y la política se mueven solos después de instalar.

### 3. Asignación de puertas

- Un `sheet` con la lista de puertas del sitio, como casillas, con la ruta del espacio al que da cada una (`useSiteStructure`).
- Las puertas que controla otro dispositivo del sitio están deshabilitadas, con "La controla {nombre}". Sale de la lista de dispositivos ya cargada.
- Guardar hace `PUT` con el conjunto completo. Un 409, porque otro la tomó mientras tanto, se muestra en el formulario y refresca la lista.

### 4. Código de reclamo

- `ClaimCodeDialog` (Radix Dialog) se abre con el resultado de la mutación de alta o de código nuevo. Muestra:
  - el código en `font-mono` grande, con espaciado;
  - un QR;
  - "Vence el 30/11/2026 14:00" en la hora del sitio;
  - "Copiar" (`navigator.clipboard.writeText`), que dice "Copiado" unos segundos;
  - el aviso "No se vuelve a mostrar. Si lo perdés, generá uno nuevo".
- El código vive solo en el estado del diálogo:
  - la mutación no guarda su resultado en la caché (`onSuccess` invalida la lista y pasa el código al diálogo);
  - al cerrar, el estado se borra;
  - no va a la dirección, ni a una clave de consulta, ni a la consola.
- **QR** (`src/components/ui/qr-code.tsx`):
  - `uqr` (versión fija) codifica el texto y devuelve la matriz.
  - El componente dibuja un `<svg>` con un `<path>` por fila, en `currentColor` sobre la tarjeta. En oscuro se invierte el contenedor a fondo claro, porque los lectores de QR necesitan contraste oscuro sobre claro.
  - `role="img"` con `aria-label="Código QR del código de reclamo"`.
  - El QR contiene exactamente el texto del código, sin URL ni otros datos: el instalador lo escanea como si lo tipeara.
- **Alternativa descartada:** `qrcode` (node-qrcode). Pesa varias veces más y genera imágenes o canvas que no siguen los tokens del tema.

### 5. Acciones con consecuencias

Los textos de confirmación dicen qué pasa:

- **Revocar:**
  - Qué pasa: el dispositivo deja de funcionar enseguida y conserva sus puertas. Para volver a usarlo hay que generar un código nuevo y reclamarlo.
  - Cuándo aparece: solo si está activo o pendiente.
- **Código nuevo:**
  - Qué pasa: el dispositivo deja de funcionar hasta que se reclame con el código nuevo.
  - Cuándo aparece: siempre.
- **Eliminar:**
  - Qué pasa: se elimina el dispositivo, deja de funcionar y sus puertas quedan libres para otro. No se puede deshacer.
  - Cuándo aparece: siempre.

### 6. Eventos: consulta, filtros y actualización

- **Filtros en la dirección:** `?puerta=`, `?persona=`, `?desde=` y `?hasta=`, con fechas en la hora del sitio como `YYYY-MM-DDTHH:mm`. Se convierten con `toSiteInstant` al pedir, así una recarga o un enlace muestran lo mismo.
  - "Persona" solo aparece si el usuario puede leer accesos. Sin ese permiso no se piden sujetos, la columna no aparece y el filtro no se ofrece.
- **Consulta:** `useInfiniteQuery` con `limit=50`, cursor en `getNextPageParam`, y la clave con el sitio y los filtros. "Cargar más eventos" pide la página siguiente; al final, "No hay más eventos".
- **Actualización automática:**
  - Una consulta aparte pide solo la primera página cada 15 s (`refetchInterval`).
  - Las condiciones: la pestaña visible (lo hace TanStack, que no refresca en segundo plano), sin `hasta` en los filtros y sin pausa.
  - Los eventos de esa página que no están en la lista se agregan arriba. Así no se vuelven a pedir todas las páginas cargadas.
  - Un evento nuevo se resalta unos segundos (`animate-rise` y fondo de acento). Una región `aria-live="polite"` anuncia "3 eventos nuevos".
  - "Pausar" y "Reanudar" cambian el estado, que no va a la dirección: es de la vista, no del filtro.
- **Columnas:**
  - hora del sitio (`dd/MM HH:mm:ss`, con la fecha completa como `title`);
  - puerta, con su nombre;
  - persona: alias, o "Sin identificar" si no hay sujeto;
  - resultado: insignia Permitido o Denegado y la razón en palabras, con las mismas cuatro razones que la prueba de acceso;
  - dispositivo: su nombre si se pueden leer dispositivos, y si no "—".
  - "Registrado sin conexión" con ícono si `receivedAt - occurredAt > 2 min`.
- Los nombres de puertas, personas y dispositivos salen de las listas del sitio ya cacheadas: el evento trae solo identificadores.

### 7. Pruebas

- La API simulada suma:
  - dispositivos (alta con código, asignación con 409, código nuevo, revocación y eliminación);
  - eventos con filtros y cursor;
  - un método para agregar eventos durante una prueba, para la actualización automática.
- Las pruebas de la actualización usan el reloj falso de Vitest (`vi.useFakeTimers({ shouldAdvanceTime: true })`) y avanzan 15 s.
- Para el código se verifica que tras cerrar el diálogo no está en el DOM, la dirección ni el almacenamiento.
- Revisión en el navegador contra la API real:
  - alta de un dispositivo con su código y QR, y asignación de puertas;
  - código nuevo y revocación.
  - Eventos: la API real no tiene ninguno sin un dispositivo físico, así que se revisa con la lista vacía y con un lote enviado con el simulador de dispositivo de las pruebas del backend, si es práctico. Si no, queda dicho.

## Risks / Trade-offs

- **Umbral de "en línea" fijo en la SPA** → es presentación. Si el intervalo de latido cambia mucho, se ajusta la constante; el cambio 17 (salud de dispositivos) puede traer el estado calculado del servidor.
- **Actualización por consulta y no por WebSocket** → un pedido liviano cada 15 s por pestaña abierta. Alcanza para la fase 1, y el cambio 16 trae el tiempo real.
- **QR del código** → expone el secreto en pantalla igual que el texto. Se muestra una sola vez y el aviso lo dice.
- **Peso**:
  - `uqr` ocupa 11,6 KB minificado (4,5 KB comprimido).
  - Con las dos pantallas, el JavaScript inicial iba a pasar de 534 KB a 628 KB. Por eso las secciones de espacios, accesos, dispositivos y eventos ahora se cargan con `lazy` al abrirlas, y el inicial queda en 503 KB. Lo había anticipado el design de 7b.
  - El service worker precarga todos los fragmentos (32 archivos), así siguen disponibles sin conexión.
- **Pruebas más lentas con la carga por sección** → con 192 pruebas en paralelo, la primera de cada archivo a veces pasaba los 5 s por prueba, porque ahora también carga su sección. El límite pasa a 15 s por prueba (`vite.config.ts`) y la espera de Testing Library a 5 s (`src/test/setup.ts`). Así pasaron cuatro corridas seguidas, sin cambiar ninguna prueba.
- **Actualización de eventos sin pedido duplicado** → la consulta de la primera página siembra la de actualización, así al entrar se hace un solo pedido, y el siguiente a los 15 s (verificado en el navegador).

## Migration Plan

Sin migración. Solo cambia la SPA, que se despliega con su build.

## Open Questions

Ninguna.
