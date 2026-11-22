# Proposal

## Why

El motor de permisos (`add-access-evaluator`, D-13) responde una sola pregunta: ¿este sujeto puede pasar por este punto de acceso? Para responderla tiene que saber qué puntos existen, a qué espacio da acceso cada uno y cómo se anidan los espacios. Hoy un sitio es una caja vacía: no hay torres, pisos, unidades, áreas comunes ni puertas. Sin ese modelo físico no se pueden escribir otorgamientos, ocupaciones (`add-occupancy-model`) ni dispositivos (`add-device-sync-protocol`), porque todos apuntan a un espacio o a un punto.

## What Changes

- Crear el módulo **Spaces** en las capas SMT (D-32), con su schema `spaces` y RLS por organización (D-28).
- **Árbol de espacios por sitio** (D-06): cada sitio tiene un espacio raíz de tipo sitio y debajo los demás nodos (torre, piso, unidad, área común, estacionamiento, lote, zona). Cada espacio tiene nombre único entre sus hermanos y la marca de espacio común (D-09).
- **Puntos de acceso**: puerta, portón, barrera, molinete o ascensor, con nombre único en el sitio, el espacio **hacia** el que da acceso (obligatorio), el espacio **desde** el que se entra (opcional) y el **modo de fallo** (fail-safe o fail-secure), que se elige siempre de forma explícita.
- **Derivación del camino**: una consulta que devuelve, para un espacio destino, los puntos de acceso que llevan hasta él. Son los puntos cuyo `hacia` es el destino o alguno de sus ancestros, ordenados desde la raíz (`docs/02-dominio-y-modelo-de-datos.md`). Es la regla de cobertura que va a usar el evaluador.
- Endpoints REST bajo `/api/v1` para crear, listar, consultar, modificar y eliminar espacios y puntos de acceso, y para consultar el camino. Se declaran en el contrato OpenAPI (D-34).
- Dos permisos administrativos nuevos, `spaces.read` y `spaces.manage`, que cubren espacios y puntos de acceso. El administrador recibe los dos y el guardia recibe la lectura. Con alcance de sitio, cubren solo ese sitio. Se modifica `role-scopes`.
- Un contrato de servicio para otros módulos (`Service.Spaces.Contracts`): existencia de espacios y puntos, y camino a un destino. Lo van a usar Access y Occupancy.

Fuera de alcance:
- **Rol con alcance de espacio** (D-03). El plan lo anotaba para este cambio. Se propone moverlo a un cambio propio, `add-space-scoped-roles` (4b), porque modifica Identity entero: asignaciones, invitaciones, la forma de los permisos efectivos y su filtrado. Ver design, decisión 8.
- Mover un espacio a otro padre. Por ahora se elimina y se vuelve a crear; el movimiento llega cuando haga falta.
- Presencia (modo suave o duro), aforo, cupo de invitados por unidad y restricción por morosidad del espacio. Llegan con `add-presence-and-incidents`, `add-strict-presence-and-capacity` y `add-delinquency-restrictions` (D-17, D-19).
- Vincular un punto de acceso con un dispositivo y su canal: `add-device-sync-protocol` (D-30).
- Otorgamientos, horarios y evaluación: `add-access-evaluator` (D-13).
- Pantallas del panel: `add-admin-panel-shell`.

## Capabilities

### New Capabilities

- `space-tree`: árbol de espacios de un sitio, con sus reglas de raíz, tipos, nombres y eliminación, y su aislamiento por organización y por alcance de sitio.
- `access-points`: puntos de acceso de un sitio, con el espacio hacia el que dan acceso, su modo de fallo y la derivación del camino a un destino.

### Modified Capabilities

- `role-scopes`: el catálogo de permisos por rol suma `spaces.read` (administrador y guardia) y `spaces.manage` (administrador).

## Impact

- Código nuevo: `Nexgate.Service.Spaces(.Contracts)`, `Nexgate.Data.Spaces`, `Nexgate.Domain.Spaces`, controllers en `Controllers/Spaces` y modelos en `Models/Spaces` de la API REST, migraciones del schema `spaces`.
- Código modificado: el catálogo de permisos de Identity, la composición de módulos de la API y del migrador, las pruebas de arquitectura (el módulo nuevo entra en sus reglas).
- Base de datos: schema `spaces` con las tablas `space` y `access_point`, y la extensión `ltree`.
- Contrato: `api/nexgate-v1.openapi.json` suma las operaciones de espacios, puntos de acceso y camino.
- **Multitenancy:** las dos tablas llevan `organization_id` con la política RLS estándar. El sitio de un espacio o punto se valida contra los sitios visibles de la organización activa. Un recurso de otra organización, o de un sitio fuera del alcance del llamador, responde igual que uno inexistente (404).
- **Seguridad:** el modo de fallo no tiene valor por defecto, para que nadie deje una salida de emergencia como fail-secure por omisión. La regla física de egreso (la puerta siempre abre desde adentro) no cambia: es de la instalación, no del software.
- **Privacidad:** espacios y puntos no contienen datos personales. Los nombres de espacios no se registran en logs, solo identificadores.
- Decisiones relacionadas: D-06, D-09, D-28, D-32, D-33, D-34. Prepara D-13 y deja D-03 (alcance de espacio) para el cambio 4b.
