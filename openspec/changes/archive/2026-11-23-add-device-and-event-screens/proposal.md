# Proposal

## Why

La API ya da de alta los dispositivos, emite su código de reclamo, les asigna puertas, los revoca y recibe sus eventos de acceso (`add-device-sync-protocol`). En la SPA, "Dispositivos" y "Eventos" dicen "Próximamente". Sin esas pantallas, un administrador tiene que llamar a la API a mano para instalar un controlador en una puerta. Y un guardia no puede ver quién entró ni quién fue rechazado. Es el cambio 7e de `docs/08`.

## What Changes

- **Pantalla de dispositivos** (con el selector de sitio de 7b, filtrado por el permiso de leer dispositivos):
  - Lista de dispositivos del sitio y detalle del elegido, que muestra:
    - estado (pendiente de reclamo, activo o revocado);
    - último contacto en palabras ("hace 3 minutos") y si está en línea;
    - si tiene la política al día o hay una versión pendiente de bajar;
    - firmware y vencimiento del certificado, con aviso si vence en menos de 30 días;
    - las puertas que controla.
  - Para quien puede gestionar dispositivos:
    - alta con nombre;
    - renombrar;
    - asignar puertas: las que controla otro dispositivo aparecen deshabilitadas, con su nombre;
    - generar un código de reclamo nuevo;
    - revocar y eliminar, con confirmación y con lo que implica cada acción explicado.
- **Código de reclamo visible una sola vez** (decisión del usuario: texto y QR):
  - Al dar de alta un dispositivo o al generar un código nuevo, se abre un diálogo con:
    - el código grande y legible;
    - un QR con el mismo texto;
    - su vencimiento en la hora del sitio;
    - un botón para copiar.
  - El diálogo avisa que el código no se vuelve a mostrar.
  - El código no queda en la dirección, en el navegador ni en la caché de datos: se descarta al cerrar el diálogo.
- **Pantalla de eventos** (filtrado por el permiso de leer eventos):
  - Los eventos del sitio, del más reciente al más viejo. Cada uno con:
    - hora del sitio;
    - puerta;
    - persona, si el usuario puede leer accesos;
    - resultado (permitido o denegado) con la razón en palabras;
    - dispositivo;
    - una marca si se registró sin conexión y llegó después.
  - Filtros por puerta, persona y rango de fechas, guardados en la dirección.
  - "Cargar más" con el cursor de la API.
  - **Actualización automática cada 15 segundos** (decisión del usuario) mientras la pestaña está visible y el rango no tiene fin. Los eventos nuevos se resaltan y se anuncian a los lectores de pantalla; un botón "Pausar" la detiene.
- **Selector de sitio**: dispositivos y eventos ofrecen solo los sitios que su permiso cubre entero, como accesos, porque un alcance de espacio no les da acceso (specs `device-provisioning` y `event-ingestion`).

Fuera de alcance:
- Eventos en tiempo real por WebSocket, que es el cambio 16 del plan, y alertas.
- Exportar eventos.
- Reportes y gráficos.
- Ver la política firmada de un dispositivo.
- Actualizaciones de firmware (OTA) y diagnóstico remoto, que son el cambio 17.
- Abrir una puerta desde la SPA.
- Cambios en la API o en el contrato OpenAPI.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `admin-shell`: suma las pantallas de dispositivos (con el código de reclamo) y de eventos. El selector de sitio filtra por permiso también en estas dos secciones.

## Impact

- **SPA** (`src/01.Client/nexgate-web`):
  - `src/features/devices/` y `src/features/events/` son nuevas.
  - `src/components/ui/qr-code.tsx` es nuevo.
  - `src/lib/relative-time.ts` es nuevo (`Intl.RelativeTimeFormat`).
  - `sections.ts` marca las dos secciones como construidas.
  - `es.json` suma los textos nuevos.
  - La API simulada suma dispositivos y eventos.
- **Dependencias**: `uqr`, con versión fija (codificador de QR sin dependencias y con licencia MIT; el peso real en el build se mide al implementar). El SVG lo dibuja la SPA con los tokens de color.
- **API, base de datos y contrato**: sin cambios.
- **Documentación**: `docs/05`, `docs/08` y el README de la SPA.
