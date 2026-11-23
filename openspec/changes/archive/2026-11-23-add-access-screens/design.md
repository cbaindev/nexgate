# Design

## Context

La SPA tiene la sección "Espacios y puertas" de `add-spaces-screens`: el selector de sitio en la dirección (`useSelectedSite`, `SitePicker`, `SiteGate`), el árbol de espacios (`SpaceTree`) y los componentes `tabs`, `table` y `confirm-dialog`.

La API de accesos ofrece estas operaciones:

| Entidad | Listar y crear | Leer, modificar y borrar |
| --- | --- | --- |
| Sujetos | `GET/POST /sites/{siteId}/subjects` | `GET/PUT/DELETE /subjects/{id}` |
| Horarios | `GET/POST /sites/{siteId}/schedules` | `GET/PUT/DELETE /schedules/{id}` |
| Credenciales | `GET/POST /sites/{siteId}/credentials?subjectId=` | `GET/PUT/DELETE /credentials/{id}`; el `PUT` cambia solo la vigencia |
| Permisos (otorgamientos) | `GET/POST /sites/{siteId}/grants?subjectId=` | `GET/DELETE /grants/{id}`; no se modifican |

La prueba es `POST /access-points/{id}/simulations`, con tipo, valor y `at`. Responde la decisión, la razón (`allowed`, `no_permission`, `explicit_deny`, `credential_invalid`) y el hash de la política.

Los formatos que importan:
- Las ventanas de un horario tienen días (`monday` a `sunday`) y horas `HH:mm`, de `00:00` a `24:00`, en la zona del sitio.
- Las vigencias son instantes `date-time`.

Los permisos `access.read` y `access.manage` valen solo con alcance de organización o de sitio: un alcance de espacio no cubre datos de acceso (spec `subjects`). Decisiones del usuario: **pantalla centrada en la persona** y **probar acceso en Accesos y en cada puerta**.

## Goals / Non-Goals

**Goals**
- Que un administrador cargue a una persona con su tarjeta y su permiso, y compruebe que pasa, sin salir de la sección.
- Que el valor de una credencial no quede en ningún lado de la SPA.
- Fechas y horas siempre en la zona del sitio.

**Non-Goals**
- Padrón de personas, invitaciones, QR, política compilada a la vista y listados globales de permisos o credenciales.

## Decisions

### 1. Estructura de la sección

- `/accesos` con `PageHeader`, el selector de sitio y pestañas en la dirección (`?vista=horarios|probar`, sin valor para Personas):
  - **Personas**: lista a la izquierda (filtro por alias, tipo con ícono) y detalle a la derecha, con el sujeto elegido en `?sujeto=<id>`. En celular, el detalle va debajo, como en espacios.
  - **Horarios**: tarjetas con el nombre y las ventanas en palabras.
  - **Probar acceso**: el formulario de prueba y su resultado.
  - En celular, las pestañas pierden el ícono para entrar en el ancho, y la lista de pestañas se desplaza en lugar de desbordar la página.
- El detalle del sujeto tiene dos bloques, "Credenciales" y "Permisos", cada uno con su botón de alta.
  - Las consultas usan el filtro `subjectId` de la API: no se trae todo el sitio para mostrar a una persona.
- Altas y ediciones en `sheet`; bajas con `confirm-dialog`. Los errores 400 van junto al campo y los 409 en un aviso dentro del formulario o del diálogo, como en 7b.
- **Alternativa descartada:** una pestaña por entidad. El usuario eligió centrar en la persona.

### 2. Selector de sitio filtrado por permiso

- `useSelectedSite({ permission })` filtra los sitios visibles por `hasPermissionOnSite(session, permission, site.id)`:
  - accesos pasa `access.read`, que exige alcance de organización o de sitio;
  - espacios y puertas sigue sin filtro: un alcance de espacio sí le sirve.
- Si el permiso no cubre ningún sitio, `SiteGate` muestra que no hay sitios disponibles para la sección, sin la acción de ir a sitios.
- Así un guardia con alcance de un espacio no ve un sitio para el que la API respondería 404.

### 3. Hora del sitio

- `src/lib/zoned-time.ts`, con `Intl.DateTimeFormat` y sin dependencias, expone:
  - `toSiteInstant(local, timeZone)`: de `YYYY-MM-DDTHH:mm`, en la zona del sitio, a un ISO UTC. Calcula el desplazamiento de la zona en ese momento y lo corrige una vez, por si el cambio de horario cae entre medio.
  - `toSiteLocal(iso, timeZone)`: de un ISO al valor de un `<input type="datetime-local">` en la zona del sitio.
  - `formatSiteDateTime(iso, timeZone)`: "01/12/2026 08:00", con `es-AR`.
- Los formularios usan `datetime-local`, con la zona del sitio indicada al lado ("Hora de America/Argentina/Cordoba").
- Pruebas con la zona del proceso de prueba forzada a otra (`TZ=Asia/Tokyo` en vitest): la conversión no depende de la zona del navegador.
- **Alternativa descartada:** una biblioteca de fechas (date-fns-tz, Luxon). `Intl` resuelve las dos conversiones que hacen falta, y la biblioteca sumaría peso.

### 4. Valor de las credenciales

- El campo del valor es `type="password"` con `autocomplete="off"` si el tipo es PIN, y texto si es tarjeta o RF: el número se lee de un lector o de la tarjeta y conviene verlo al cargarlo. En los dos casos lleva `autocomplete="off"` y `spellcheck={false}`.
- El valor vive solo en el estado del formulario. Al guardar con éxito el `sheet` se cierra y el formulario se desmonta. Si falla, queda para corregirlo.
- No se registra en consola, no va a la dirección ni a TanStack Query: la mutación lo recibe como variable y su clave no lo incluye.
- La prueba de acceso hace lo mismo: el resultado se muestra sin el valor, y "Probar otra" limpia el campo.
- Una prueba de la SPA verifica que tras registrar una credencial el valor no aparece en el DOM, la dirección ni el almacenamiento.

### 5. Credenciales y permisos en el detalle

- **Credencial:**
  - Ícono por tipo; estado calculado contra ahora: vigente, por empezar o vencida, con una insignia de color.
  - Vigencia "desde … hasta …" en hora del sitio, o "sin vencimiento".
  - Acciones: cambiar vigencia (`PUT`) y eliminar.
- **Permiso:**
  - Insignia "Permite" (éxito) o "Deniega" (peligro).
  - El destino con su ruta: un espacio con `SpaceTree.path`, una puerta con su nombre y el espacio al que da.
  - Horario por nombre o "Siempre"; vigencia; usos restantes si tiene máximo.
  - Origen si no es `manual`.
  - Acción: eliminar. No hay edición porque la API no la ofrece: se elimina y se crea otro.
- **Formulario de permiso:**
  - Efecto en radios: Permitir, o Denegar, con la explicación de que la denegación gana siempre.
  - Destino: un desplegable agrupado ("Espacios", con la ruta de cada uno, y "Puertas"), con los datos de espacios y puertas del sitio que ya usa 7b.
  - Horario opcional, vigencia opcional y máximo de usos, solo si permite.
- El árbol y las puertas se piden con las mismas claves de consulta de 7b, así quedan en caché entre secciones.

### 6. Horarios

- **Ventanas en palabras.** Días consecutivos se juntan en rangos ("lunes a viernes"), los demás se enumeran ("lunes, miércoles y viernes"), y todos los días es "Todos los días". `24:00` se muestra como "24:00".
- **Editor**:
  - Una lista de ventanas, cada una con siete casillas de días (L M M J V S D, con su nombre completo accesible) y dos campos `type="time"`.
  - "Agregar ventana" hasta 20, el máximo de la API.
  - El fin del día se marca con "hasta el final del día", que envía `24:00`, porque un campo de hora no admite 24:00.
- Validación en el cliente antes de enviar: al menos un día, y fin posterior al inicio. Las demás reglas las decide el servidor.

### 7. Prueba de acceso

- **Formulario:** puerta (desplegable con la ruta de su espacio), tipo, valor (oculto si es PIN) y momento: "Ahora", o una fecha y hora del sitio.
- **Resultado** en una tarjeta grande: "Pasaría" (éxito) o "No pasaría" (peligro), con la razón en palabras:
  - `allowed`: un permiso lo habilita en ese momento.
  - `no_permission`: ningún permiso vigente lo habilita a esa hora en esa puerta.
  - `explicit_deny`: una denegación gana sobre cualquier permiso.
  - `credential_invalid`: la credencial no existe en el sitio, venció o todavía no empieza.
- Debajo, en texto chico, va que la prueba no abre la puerta, no queda registrada y no consume usos.
- `SimulationForm` es un componente de `src/features/access`:
  - en la pestaña, con la puerta elegible;
  - en "Espacios y puertas", dentro de un `sheet` abierto desde cada fila, con la puerta fija.
  - La tabla de puertas muestra el botón solo con `access.read` sobre el sitio entero.

### 8. Pruebas

- La API simulada suma sujetos, horarios, credenciales (guarda tipo y valor para detectar duplicados y simular), permisos y la simulación:
  - credencial desconocida, vencida o por empezar;
  - permiso que cubre la puerta por el camino de acceso;
  - horario en la hora del sitio;
  - denegación que gana.
- Una prueba por escenario del spec, más las de `zoned-time` con la zona del proceso distinta a la del sitio.
- Revisión en el navegador contra la API real: cargar una persona con tarjeta y permiso a 5B en horario, y probar en la puerta de 5B dentro y fuera de horario.

## Risks / Trade-offs

- **Peso del bundle** → el JavaScript queda en 534 KB, casi igual que en 7b: la sección no suma dependencias.
- **Pruebas más lentas** → con 165 pruebas en paralelo, la primera de cada archivo a veces superaba la espera de 1 s de Testing Library. La espera pasa a 3 s en `src/test/setup.ts`, sin cambiar ninguna prueba.
- **La simulación de la API simulada repite reglas de la evaluación** → solo lo mínimo para que la pantalla muestre cada razón. La regla real tiene sus propias pruebas en el backend, y la revisión en el navegador usa la API real.
- **El valor de una tarjeta se ve al cargarlo** → hace falta para comprobar lo leído. Se descarta al guardar y nunca se vuelve a mostrar; el PIN va oculto.
- **Conversión de zona sin biblioteca** → acotada a dos funciones con pruebas en otra zona horaria y alrededor de un cambio de horario.

## Migration Plan

Sin migración. Solo cambia la SPA, que se despliega con su build.

## Open Questions

Ninguna.
