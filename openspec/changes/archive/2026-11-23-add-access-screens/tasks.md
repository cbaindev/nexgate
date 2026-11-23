# Tasks

## 1. Base

- [x] 1.1 `src/lib/zoned-time.ts` (instante desde hora del sitio, hora del sitio desde instante y formato); verificar con pruebas en otra zona horaria y alrededor de un cambio de horario
- [x] 1.2 `useSelectedSite({ permission })` que filtra los sitios por permiso; verificar con la prueba de `Access section with a space scope` y las de 7b en verde
- [x] 1.3 API simulada con sujetos, horarios, credenciales, permisos y simulación (razones, horario en hora del sitio, denegación que gana, conflictos 409); verificar con las pruebas existentes en verde

## 2. Personas

- [x] 2.1 Sección `/accesos` con pestañas en la dirección y marcada como construida, lista de sujetos con filtro y sujeto elegido en la dirección; verificar con pruebas de `List subjects` y de navegación
- [x] 2.2 Alta, edición y baja de sujetos con la advertencia de datos personales y el conflicto 409; verificar con pruebas de `No personal data` y `Subject in use`
- [x] 2.3 Credenciales en el detalle (estado, vigencia en hora del sitio, alta con valor que se descarta, PIN oculto, cambio de vigencia y baja); verificar con pruebas de `Register a credential`, `PIN is hidden while typing` y `Duplicate credential`
- [x] 2.4 Permisos en el detalle (efecto, destino con ruta, horario, vigencia, usos y baja) y su formulario; verificar con pruebas de `Grant to a unit within a schedule`, `Validity in the time zone of the site` y `Deny grant`

## 3. Horarios

- [x] 3.1 Lista con ventanas en palabras y editor de ventanas (días, horas, hasta el final del día, validación); verificar con pruebas de los escenarios de `Schedules screen`

## 4. Prueba de acceso

- [x] 4.1 `SimulationForm` en la pestaña "Probar acceso", con la razón en palabras y el aviso de que no es un evento; verificar con pruebas de `Allowed pass`, `Denied pass with its reason`, `Deny wins` y `Test is not an event`
- [x] 4.2 "Probar acceso" en cada puerta de "Espacios y puertas", solo con permiso de leer accesos; verificar con pruebas de `Test from an access point` y `Without access permission`

## 5. Verificación y documentación

- [x] 5.1 Revisión en el navegador con la API real: como administrador, persona con tarjeta y permiso a una unidad en horario, prueba dentro y fuera de horario desde una puerta y un 409 de credencial repetida; como guardia, la sección sin acciones (con el sitio todavía vacío); claro, oscuro y celular. El teclado y el guardia con datos quedan cubiertos por las pruebas de la SPA, no por la revisión manual
- [x] 5.2 README de la SPA (hora del sitio, valor de las credenciales, selector por permiso), `docs/05` y `docs/08` (7d implementado); verificar releyendo contra los specs
- [x] 5.3 Verificación final: `npm run check` en verde, y `dotnet build` y `dotnet test` en verde (sin cambios en el backend)
