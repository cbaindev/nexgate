# Tasks

## 1. Base

- [x] 1.1 `src/lib/relative-time.ts` y `qr-code` (con `uqr` en versión fija) sumado a `/_design`; verificar con pruebas unitarias del tiempo relativo y de que el QR dibuja una matriz cuadrada con nombre accesible
- [x] 1.2 API simulada con dispositivos (alta con código, asignación con 409, código nuevo, revocación, eliminación) y eventos (filtros, cursor, agregar durante la prueba); verificar con las pruebas existentes en verde

## 2. Dispositivos

- [x] 2.1 Sección `/dispositivos` marcada como construida, selector por `devices.read`, lista y detalle con estado, conexión, política, firmware, certificado y puertas; verificar con pruebas de `List devices`, `Device details`, `Device without contact`, `Certificate about to expire` y `Devices and events with a space scope`
- [x] 2.2 Alta y renombre, código de reclamo en diálogo con QR, copiar y vencimiento, que se descarta al cerrar; verificar con pruebas de los escenarios de `Claim code presentation`
- [x] 2.3 Asignación de puertas con las de otro dispositivo deshabilitadas, código nuevo, revocación y eliminación con confirmación; verificar con pruebas de `Assign access points`, `Access point of another device`, `Revoke with confirmation` y `Read-only users`

## 3. Eventos

- [x] 3.1 Sección `/eventos` marcada como construida, tabla con hora del sitio, puerta, persona, resultado, dispositivo y marca de sin conexión; verificar con pruebas de `List events`, `Subject without access permission` y `Event recorded offline`
- [x] 3.2 Filtros en la dirección y "Cargar más" con cursor; verificar con pruebas de `Filter by access point and dates` y `Load more`
- [x] 3.3 Actualización cada 15 s con eventos nuevos resaltados y anunciados, y pausa; verificar con pruebas de `New events arrive` y `Paused` con reloj falso

## 4. Verificación y documentación

- [x] 4.1 Revisión en el navegador con la API real como administrador: alta de un dispositivo con código y QR, asignación de puertas, revocación y código nuevo; eventos con la lista vacía (la base de desarrollo no tiene eventos y mandarlos exige reclamar un dispositivo con certificado de cliente, que no es práctico desde la revisión), verificando que la actualización pide una vez al entrar y otra a los 15 s; claro, oscuro y celular. El guardia y los eventos con datos quedan cubiertos por las pruebas de la SPA
- [x] 4.2 README de la SPA (código de reclamo, actualización de eventos), `docs/05` y `docs/08` (7e implementado); verificar releyendo contra los specs
- [x] 4.3 Verificación final: `npm run check` en verde, y `dotnet build` y `dotnet test` en verde (sin cambios en el backend)
