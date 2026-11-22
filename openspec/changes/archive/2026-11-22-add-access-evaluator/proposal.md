# Proposal

## Why

El corazón de Nexgate es responder "¿esta credencial abre este punto de acceso ahora?" con una sola regla, la misma en el backend y en el firmware, y con la denegación ganando siempre (D-13). Ya existen el árbol de espacios, los puntos de acceso y el camino a un destino (cambios 4 y 4b). Falta lo que se evalúa: quién (sujeto y credencial), a qué (otorgamiento a un espacio o a un punto), cuándo (horario y vigencia) y el algoritmo que decide. `docs/08` pide hacerlo apenas existan tenancy y espacios, con los vectores de prueba pensados desde el principio para que el firmware los reutilice.

## What Changes

- **Sujetos mínimos** (módulo Occupancy, adelantado): un sujeto es la representación local de alguien en un sitio, con un alias y un tipo (persona o anónimo), sin datos personales ni vínculo con el padrón. `add-people-registry` y `add-occupancy-model` lo completan después. Es lo que referencian las credenciales y los otorgamientos (regla del proyecto: los datos de negocio solo referencian `SujetoId`).
- **Módulo Access**, con:
  - **Horarios** por sitio: franjas por día de la semana en la zona horaria del sitio.
  - **Credenciales** de un sujeto: control RF, tarjeta o PIN, con vigencia. El valor nunca se guarda ni se devuelve en claro: se guarda un HMAC con una clave por sitio.
  - **Otorgamientos** (D-13): sujeto → destino (un espacio o un punto de acceso) → horario opcional → vigencia → efecto (permitir o denegar) → origen, con usos máximos opcionales. En este cambio el único origen es `manual`; `ocupacion`, `invitacion` y `rol` llegan con sus cambios.
  - **Cobertura**: un otorgamiento a un espacio cubre los puntos del camino a ese espacio (los que dan acceso a él o a sus ancestros, `docs/02`); un otorgamiento a un punto cubre solo ese punto.
  - **Compilación por punto de acceso**: la lista de credenciales que pueden abrir ese punto, con sus ventanas horarias, vigencias, usos y denegaciones, en un formato versionado con hash. El cambio 6 la empaqueta por dispositivo, la firma y la publica.
  - **Evaluación**: el algoritmo del documento 03 sobre la política compilada, sin red. El backend lo usa para simular una pasada (endpoint de simulación) y el firmware usará el mismo algoritmo. La respuesta dice el motivo: credencial inválida, denegación explícita, sin permiso o permitido.
  - **Vectores de prueba compartidos**: archivos JSON en el repositorio con una política compilada, una pasada y la decisión esperada. Las pruebas del backend los ejecutan; el firmware va a ejecutar los mismos.
- **Permisos** nuevos: `access.read` (administrador y guardia) y `access.manage` (administrador), con alcance de sitio entero.
- **Integridad entre módulos**: no se elimina un espacio ni un punto de acceso al que apunta un otorgamiento, ni un sujeto con credenciales u otorgamientos (puertos de uso, como en el cambio 4b).

Fuera de alcance:
- Plantillas por rol de ocupación, unidades, ocupaciones y membresías (`add-occupancy-model`), y el vínculo del sujeto con el padrón (`add-people-registry`).
- Orígenes `ocupacion`, `invitacion` y `rol`, y la revocación en cascada por origen: llegan con los cambios que crean esos orígenes.
- Credenciales QR firmadas (`add-signed-qr-credentials`) y BLE (`add-ble-credentials`).
- Morosidad (`add-delinquency-restrictions`) y presencia o antipassback (`add-presence-and-incidents`, `add-strict-presence-and-capacity`): el algoritmo deja su lugar previsto pero no los evalúa.
- Descontar usos: lo hace la ingesta de eventos (`add-device-sync-protocol`). Acá los usos restantes solo se compilan y se evalúan.
- Versión monotónica persistida, recompilación por eventos, empaquetado por dispositivo, firma y publicación de la política (`add-device-sync-protocol`).
- Alcance de espacio para los permisos de Access: un usuario con alcance de espacio no ve sujetos, credenciales ni otorgamientos (se revisa cuando haya un caso real, por ejemplo el guardia de una torre).
- Pantallas del panel.

## Capabilities

### New Capabilities

- `subjects`: registro mínimo de sujetos de un sitio, sin datos personales.
- `access-grants`: horarios, credenciales y otorgamientos de un sitio, con sus validaciones, su cobertura y su aislamiento.
- `policy-compilation`: la política compilada de un punto de acceso, su formato y su hash.
- `access-evaluation`: el algoritmo de decisión sobre la política compilada, la simulación por API y los vectores de prueba compartidos con el firmware.

### Modified Capabilities

- `role-scopes`: el catálogo de permisos por rol suma `access.read` y `access.manage`.
- `space-tree`: un espacio destino de un otorgamiento no se puede eliminar.
- `access-points`: un punto de acceso destino de un otorgamiento no se puede eliminar.

## Impact

- Código nuevo: módulos Occupancy (solo sujetos) y Access en las cuatro capas, controllers y modelos en la API REST, migraciones de los schemas `occupancy` y `access`, vectores de prueba en `vectors/access-evaluation/`.
- Código modificado: catálogo de permisos de Identity; `ISiteDirectory` suma la zona horaria del sitio; `Service.Spaces.Contracts` suma el puerto de uso de puntos de acceso; composición de módulos y reglas de arquitectura.
- Dependencias entre módulos: `Service.Access → Service.Spaces.Contracts`, `Service.Occupancy.Contracts` y `Service.Tenancy.Contracts`; `Service.Occupancy → Service.Tenancy.Contracts`. Ningún módulo anterior depende de Access.
- Configuración: `Access:CredentialKey`, la clave maestra de la que se deriva la clave HMAC de cada sitio. En `Development` hay una clave fija de desarrollo; fuera de `Development` es obligatoria y no puede ser la de desarrollo.
- Contrato OpenAPI: operaciones de sujetos, horarios, credenciales, otorgamientos, política y simulación.
- **Multitenancy:** todas las tablas nuevas llevan `organization_id` con la política RLS estándar. Sujeto, destino y horario de un otorgamiento se validan contra el mismo sitio; lo de otra organización o de otro sitio responde como inexistente.
- **Seguridad:** el valor de una credencial solo existe en la solicitud que la crea o la simula; se guarda su HMAC con clave por sitio y nunca se devuelve. La denegación gana siempre, también frente a otorgamientos de permitir más amplios.
- **Privacidad:** el sujeto no tiene datos personales (el alias lo elige quien lo carga y no se registra en logs). Ni el valor de las credenciales ni su HMAC se registran en logs ni trazas.
- Decisiones relacionadas: D-13 (implementa), D-06, D-15 (cobertura por camino), D-28. Prepara D-14, D-16, D-17 y D-19.
