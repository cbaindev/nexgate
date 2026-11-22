# Design

## Context

Existen Tenancy (sitios con zona horaria IANA), Identity (roles por alcance de organización, sitio o espacio) y Spaces (árbol con `path` `ltree`, puntos de acceso, camino a un destino y puerto de uso `ISpaceUsage`). `docs/03` define el otorgamiento, el algoritmo de evaluación y la compilación hacia los dispositivos; `docs/02` ubica el sujeto en Occupancy y las credenciales, horarios y otorgamientos en Access. Ver `proposal.md` para la motivación, el alcance y las dos decisiones tomadas con el usuario: adelantar un registro mínimo de sujetos y compilar por punto de acceso.

## Goals / Non-Goals

**Goals**
- Un único algoritmo de decisión, puro y sin red, que el backend y el firmware ejecuten sobre la misma política compilada.
- Vectores de prueba en un formato independiente del backend.
- Que la cobertura salga del árbol de espacios (D-15): nadie elige puertas.

**Non-Goals**
- Persistir versiones, publicar ni firmar políticas; empaquetar por dispositivo.
- Orígenes distintos de `manual`, morosidad, presencia y descuento de usos.

## Decisions

### 1. Sujetos mínimos en el módulo Occupancy

Se crea el módulo Occupancy con una sola tabla, `occupancy.subject` (id, organization_id, site_id, kind: person | anonymous, alias, created_by, created_at, updated_at). `Service.Occupancy.Contracts` expone `ISubjectDirectory` (existencia y sitio de un sujeto) y el puerto `ISubjectUsage`, que Access implementa para impedir eliminar un sujeto con credenciales u otorgamientos.

- Respeta `docs/02` (el sujeto vive en Occupancy) y la regla de que los datos de negocio solo referencian `SujetoId`. `add-occupancy-model` suma unidades, ocupaciones y membresías al mismo módulo, y `add-people-registry` el vínculo opaco con el padrón.
- El alias es una etiqueta que elige quien carga el sujeto ("Empleada del 5B"). No se registra en logs ni trazas.
- **Alternativa descartada:** sujetos dentro de Access. Mezcla responsabilidades y obligaría a mudar la tabla con datos cuando llegue Occupancy.
- **Alternativa descartada:** identificador opaco sin tabla. La API aceptaría cualquier identificador y no habría prueba de punta a punta.

### 2. Credenciales con HMAC por sitio

- Valor normalizado: sin espacios alrededor; para `rf` y `card`, en mayúsculas (son códigos hexadecimales o decimales según el lector); `pin` se guarda como se escribe.
- Clave del sitio: `HMAC-SHA256(claveMaestra, "nexgate/credential-site/v1/" + siteId)`. Hash guardado: `HMAC-SHA256(claveDelSitio, tipo + ":" + valorNormalizado)` (32 bytes, `bytea`). La clave maestra es `Access:CredentialKey` (base64, al menos 32 bytes).
- La política compilada lleva ese mismo hash (base64url). Los dispositivos de un sitio recibirán la clave del sitio al aprovisionarse (cambio 6) y calcularán el mismo HMAC sobre lo que leen.
- Índice único `(site_id, type, value_hash)`: no hay dos credenciales iguales en un sitio.
- `docs/03` decía "HMAC con clave del dispositivo". Con una clave por dispositivo, el backend necesitaría el valor en claro (o cifrado reversible) para recompilar cada vez. **Alternativa descartada:** guardar el valor cifrado de forma reversible: más datos sensibles en reposo, para un beneficio menor (aislar dispositivos del mismo sitio, que igual abren las mismas puertas). Se actualiza `docs/03`.
- La rotación de la clave maestra (recalcular todos los hashes) queda fuera de alcance.
- Configuración: en `Development` hay una clave fija en `appsettings.Development.json`; fuera de `Development` el arranque falla si falta o si es la de desarrollo (mismo criterio que la clave de firma de tokens). Las pruebas usan su propia clave.

### 3. Horarios

`access.schedule` (id, organization_id, site_id, name, windows `jsonb`, timestamps). Cada ventana: `days` (lista de `monday` … `sunday`), `from` y `to` en `HH:mm`, con `from < to` y `to` hasta `24:00`. Una franja nocturna (22 a 6) se carga como dos ventanas. Hasta 20 ventanas. Nombre único por sitio sin distinguir mayúsculas.

- **Alternativa descartada:** tabla de ventanas aparte. Las ventanas siempre se leen y escriben juntas con su horario.

### 4. Otorgamientos

`access.grant` (id, organization_id, site_id, subject_id, target_type: space | access_point, target_id, schedule_id (FK con `ON DELETE RESTRICT`), valid_from, valid_until, effect: allow | deny, origin_type (`manual`), origin_id, remaining_uses, created_by, created_at).

- El sujeto se valida con `ISubjectDirectory`, el destino con `ISpaceDirectory` (mismo sitio) y el horario en el propio módulo. Fuera del sitio o de la organización: 400 con el campo, igual que inexistente.
- `remaining_uses` nace igual al máximo indicado; solo se permite en otorgamientos `allow`.
- No hay modificación de otorgamientos en este cambio: se eliminan y se crean de nuevo. Así la auditoría futura (por origen) no tiene que reconstruir ediciones.
- Access implementa `ISpaceUsage` y el nuevo `IAccessPointUsage` (en `Service.Spaces.Contracts`): un espacio o punto destino de un otorgamiento, vencido o no, no se elimina.

### 5. Cobertura con el subárbol

Un otorgamiento a un espacio S cubre el punto P si el destino de P está en el camino a S, es decir, si S es el destino de P o un descendiente suyo. Para compilar P:

- `ISpaceDirectory.GetSubtreeIdsAsync(spaceId)` (nuevo) devuelve el destino de P y sus descendientes con `path <@ destino.path` (usa el índice GiST que dejó el cambio 4).
- Otorgamientos que cubren P = los del sitio con `target_type = space` y destino en ese subárbol, más los de `target_type = access_point` y destino P.

### 6. Política compilada (formato v1)

```json
{
  "format": 1,
  "siteId": "…", "accessPointId": "…",
  "timeZone": "America/Argentina/Cordoba",
  "offsets": [ { "from": null, "seconds": -10800 } ],
  "windows": [ [ { "days": 31, "from": 480, "to": 1020 } ] ],
  "credentials": [ { "hash": "base64url", "from": 1767225600, "to": null } ],
  "entries": [ { "credential": 0, "window": 0, "from": null, "to": null, "uses": null, "deny": false } ],
  "hash": "sha256 hex"
}
```

- `offsets`: desplazamiento UTC vigente al compilar (`from: null`) y cada transición de los próximos 400 días (segundos Unix). El dispositivo calcula la hora local sin base de zonas horarias. Para zonas sin horario de verano queda una sola fila y el hash no cambia con los días.
- `windows`: un horario es una lista de ventanas; `days` es una máscara (bit 0 = lunes … bit 6 = domingo), `from` y `to` en minutos del día, intervalo `[from, to)`.
- `credentials`: todas las credenciales activas del sitio (no vencidas al compilar), con su vigencia. `entries` las referencian por índice.
- Entrada: intersección de la vigencia de la credencial y la del otorgamiento; `window` nulo es "siempre"; `uses` nulo es sin límite.
- Orden canónico: credenciales por hash, ventanas por primer uso, entradas por (credencial, deny, window, from, to, uses). El hash es SHA-256 del JSON sin `hash`, sin espacios y con las propiedades en el orden del ejemplo.
- *Ajustado durante la implementación:* se incluyen todas las credenciales activas del sitio, también las que no tienen entradas en ese punto, como dice el spec. Así una credencial válida sin permiso en esa puerta da `no_permission` y no `credential_invalid`: el evento distingue una tarjeta desconocida de una que no tiene permiso ahí. No revela nada nuevo: los dispositivos del sitio comparten la clave del sitio (decisión 2). Un otorgamiento que no cubre el punto no cambia su hash; dar de alta o eliminar una credencial en el sitio sí cambia el de todos sus puntos.
- **Alternativa descartada:** incluir solo las credenciales con entradas. Política más chica, pero pierde la distinción entre credencial desconocida y sin permiso.
- **Alternativa descartada:** compilar por sitio en vez de por punto. Un dispositivo controla pocos puntos y la política por punto es lo que hay que cargar; el cambio 6 junta las de los puntos de cada dispositivo.

### 7. Algoritmo de decisión

`AccessDecision.Evaluate(policy, credentialHash, instantUtc)` en `Domain.Access`, puro:

1. Buscar la credencial; si no está o el instante cae fuera de `[from, to)`, `credential_invalid`.
2. Calcular la hora local con `offsets` (la última fila cuyo `from` es nulo o menor o igual que el instante): día de la semana y minuto del día.
3. Entradas de esa credencial que aplican: instante dentro de `[from, to)` y, si tienen ventana, alguna ventana del horario contiene día y minuto.
4. Si alguna que aplica deniega, `explicit_deny`.
5. Si alguna que aplica permite y `uses` es nulo o mayor que 0, `allowed`.
6. Si no, `no_permission`.

- Diferencia con `docs/03`: allí la denegación se evalúa antes de filtrar por horario. Una denegación con horario ("no los fines de semana") debe aplicar solo dentro de su horario, así que la denegación se filtra por vigencia y ventana igual que el permiso. Se actualiza `docs/03`.
- Morosidad (paso 5 de `docs/03`) y antipassback (paso 6) quedan para sus cambios; el algoritmo los agregará entre los pasos 4 y 5.

### 8. Vectores de prueba compartidos

`vectors/access-evaluation/v1/*.json`, uno por caso, con `description`, `policy` (formato v1), `credentialHash`, `at` (ISO 8601 UTC) y `expected` (`decision`, `reason`). Un `README.md` en la carpeta describe el formato en español. La prueba `AccessVectorsTests` recorre todos los archivos y falla si la carpeta está vacía. Los vectores cubren cada escenario de `access-evaluation`, más límites (inicio y fin de vigencia, minuto 0 y 1439, domingo, transición de horario de verano).

### 9. Simulación y política por API

| Método y ruta | Operación | Permiso |
|---|---|---|
| `GET/POST /api/v1/sites/{siteId}/subjects` | `listSubjects`, `createSubject` | `access.read` / `access.manage` |
| `GET/PUT/DELETE /api/v1/subjects/{id}` | `getSubject`, `updateSubject`, `deleteSubject` | ídem |
| `GET/POST /api/v1/sites/{siteId}/schedules` | `listSchedules`, `createSchedule` | ídem |
| `GET/PUT/DELETE /api/v1/schedules/{id}` | `getSchedule`, `updateSchedule`, `deleteSchedule` | ídem |
| `GET/POST /api/v1/sites/{siteId}/credentials` (filtro `subjectId`) | `listCredentials`, `createCredential` | ídem |
| `GET/PUT/DELETE /api/v1/credentials/{id}` | `getCredential`, `updateCredential` (solo vigencia), `deleteCredential` | ídem |
| `GET/POST /api/v1/sites/{siteId}/grants` (filtro `subjectId`) | `listGrants`, `createGrant` | ídem |
| `GET/DELETE /api/v1/grants/{id}` | `getGrant`, `deleteGrant` | ídem |
| `GET /api/v1/access-points/{id}/policy` | `getAccessPointPolicy` | `access.read` |
| `POST /api/v1/access-points/{id}/simulations` | `simulateAccess` | `access.read` |

- Simulación: `{ credentialType, credentialValue, at? }` → `{ decision, reason, policyHash }`. Compila la política vigente, calcula el hash de la credencial y evalúa. No registra eventos ni descuenta usos. El cuerpo de la solicitud no se registra (ya no se registran cuerpos; se agrega una prueba que lo verifica con el valor de la credencial).
- Permisos (`Permissions.AccessRead`, `Permissions.AccessManage` en `Identity.Contracts`): exigen el sitio entero (`PermissionScope.CoversSite`); fuera de la lectura, 404; dentro de la lectura sin gestión, 403. La política y la simulación usan el sitio del punto de acceso.
- `ISiteDirectory` suma `GetTimeZoneAsync(siteId)` para compilar.

### 10. Tablas y aislamiento

Schemas `occupancy` y `access`. Todas las tablas (`subject`, `schedule`, `credential`, `grant`) llevan `organization_id` con `EnableTenantIsolation` y `GrantAppAccess`. Claves foráneas solo dentro del schema (`grant.schedule_id`); sujeto, espacio y punto se validan por contrato (sin claves entre schemas, `docs/05`). La prueba de catálogo cubre los schemas nuevos.

## Cómo se prueba

- **Unitarias (Domain.Access):** validación de horarios, normalización y HMAC de credenciales, intersección de vigencias, orden canónico y hash, algoritmo de decisión.
- **Vectores compartidos:** `AccessVectorsTests` ejecuta todos los archivos de `vectors/access-evaluation/v1`.
- **Integración con PostgreSQL real:** escenarios de los specs por HTTP con el edificio de ejemplo (raíz, torres A y B, piso 5, 5B), incluida la cobertura por camino, la simulación de punta a punta, la política con su hash estable, 404 idénticos, permisos y alcance, y la eliminación bloqueada de espacios, puntos, horarios y sujetos.
- **Aislamiento:** SQL directo como `nexgate_app` sobre las tablas nuevas con dos organizaciones.
- **Privacidad:** una prueba captura los logs de una simulación y de un alta de credencial y verifica que no aparecen el valor, el hash ni el alias.
- **Arquitectura y contrato:** los módulos nuevos entran en las reglas; el documento OpenAPI se regenera.

## Risks / Trade-offs

- **Clave por sitio en vez de por dispositivo:** un dispositivo comprometido revela la clave del sitio. Mitigación: flash cifrada y arranque seguro (`docs/04`); rotación de la clave como cambio futuro.
- **Compilación bajo demanda:** cada simulación compila la política del punto. Para un edificio es barato; la compilación persistida e incremental llega con el cambio 6.
- **Hash que depende del instante:** los otorgamientos vencidos salen de la política y las transiciones de horario de verano entran a los 400 días; el hash cambia aunque nadie toque nada. Es correcto (el contenido cambió) y el cambio 6 decide cuándo recompilar.
