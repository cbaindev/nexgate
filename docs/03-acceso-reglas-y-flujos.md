# 03 Acceso, reglas y flujos

## 1. Un solo concepto: el Otorgamiento

Todo permiso, sea de un residente, un empleado o un invitado, es un registro del mismo tipo:

> **Sujeto → Destino (espacio o punto) → Horario → Vigencia → Efecto (permitir o denegar) → Origen**

El **origen** dice por qué existe: `ocupacion`, `invitacion`, `rol` o `manual`. Gracias a eso, cuando el origen termina, se revoca en cascada todo lo que dependía de él (por ejemplo, al cerrar una ocupación caen también las invitaciones de ese owner).

## 2. Evaluación: ¿puede pasar?

```
puede_pasar(credencial, punto_acceso, instante):
  1. La credencial debe estar activa y vigente.            → si no, DENEGAR (credential_invalid)
  2. Reunir los otorgamientos del sujeto cuyo destino
     cubre al punto de acceso y que aplican en el instante:
     vigencia y horario (zona horaria del sitio).
  3. Si alguno de ellos tiene efecto DENEGAR.              → DENEGAR (explicit_deny)
  4. Descartar los permisos sin usos restantes.
  5. Descartar los condicionados por morosidad (ver 6).
  6. Si la zona tiene presencia en modo duro y el sujeto
     ya figura dentro.                                     → DENEGAR (antipassback)
  7. Si queda al menos un permiso.                         → PERMITIR (allowed)
     Si no.                                                → DENEGAR (no_permission)
```

La denegación también respeta su horario: una regla "no los fines de semana" aplica solo el fin de semana, y dentro de su horario gana siempre (D-13). Los pasos 5 y 6 llegan con `add-delinquency-restrictions` y `add-presence-and-incidents`; `add-access-evaluator` implementa el resto.

**Cuándo un otorgamiento cubre un punto:** si el `hacia` del punto es el destino del otorgamiento o uno de sus ancestros (el camino al destino, ver documento 02). Un otorgamiento a un destino que es un **punto** cubre solo ese punto.

Este mismo algoritmo corre en dos lugares: en el backend (para simulaciones y auditoría) y en el firmware (con la política compilada, sin red). Los dos lo ejecutan sobre la política compilada y se prueban con los mismos vectores (`vectors/access-evaluation/`).

## 3. Compilación de políticas hacia los dispositivos

El backend no manda "reglas" a las puertas: manda una **política compilada** por dispositivo.

- Un compilador toma los otorgamientos que no vencieron y calcula, para cada punto de acceso, la lista de credenciales que pueden abrir y en qué ventanas (`add-access-evaluator`). El dispositivo junta las políticas de sus puntos (`add-device-sync-protocol`).
- Cada política tiene un hash de su contenido canónico; el número de **versión monotónico** lo agrega el cambio de dispositivos. El dispositivo informa qué versión tiene; si hay una más nueva, la descarga (completa o como delta).
- Se recompila **solo lo afectado**: un evento interno (outbox) como "otorgamiento creado", "ocupación cerrada" o "estado financiero cambiado" dispara la recompilación de los dispositivos involucrados.
- Las credenciales se guardan como **HMAC con una clave por sitio**, derivada de una clave maestra del backend, nunca en claro. Los dispositivos del sitio reciben la clave del sitio al aprovisionarse y calculan el mismo HMAC sobre lo que leen. Se descartó la clave por dispositivo: obligaba a guardar el valor de las credenciales de forma reversible para recompilar.

Formato v1 de la política de un punto de acceso (detalle en `vectors/access-evaluation/README.md`):

```json
{
  "format": 1, "siteId": "…", "accessPointId": "…", "timeZone": "America/Argentina/Cordoba",
  "offsets": [ { "from": null, "seconds": -10800 } ],
  "windows": [ [ { "days": 31, "from": 480, "to": 1020 } ] ],
  "credentials": [ { "hash": "<hmac base64url>", "from": null, "to": null } ],
  "entries": [ { "credential": 0, "window": 0, "from": null, "to": null, "uses": null, "deny": false } ],
  "hash": "<sha256>"
}
```

`offsets` permite calcular la hora local sin base de zonas horarias; `windows` son horarios (máscara de días, bit 0 = lunes, y minutos locales `[from, to)`); `credentials` son todas las credenciales activas del sitio, así una credencial válida sin permiso en esa puerta se distingue de una desconocida; cada entrada une una credencial con un otorgamiento que cubre el punto, con la intersección de vigencias y los usos restantes.

## 3.1 Credenciales sin lista: QR firmado

Para invitados temporales, el QR contiene un token firmado (id, vencimiento, destino, usos) con la clave del sitio. El dispositivo lo **verifica sin conexión** con la clave pública y revisa una pequeña lista de revocación. Así el delivery entra sin que haya que sincronizar nada antes.

## 4. Flujos

### 4.1 Alta de residentes (administrador)

1. El administrador crea o elige la **Unidad** y la asocia a un **Espacio** con una **Ocupación** (rol y vigencia).
2. Agrega las personas como **Membresía** con rol (titular, cónyuge, hijo...). El sistema vincula cada una al padrón por documento.
3. Se asocian credenciales (control RF, tarjeta, BLE, PIN).
4. Se generan los otorgamientos desde la **plantilla de acceso del rol**.
5. Se recompilan y publican las políticas de los dispositivos afectados.

### 4.2 Invitación nominal o recurrente (owner)

1. El owner elige un contacto de su **libreta** (o carga uno nuevo: nombre, documento, teléfono).
2. Elige **destino** (su espacio, o un espacio común) y **horario**. Para una empleada doméstica usa una plantilla: "lunes a viernes de 8 a 17".
3. El sistema deriva las puertas del camino y crea el otorgamiento con origen `invitacion`.
4. Se envía un link o QR por WhatsApp, SMS o mail.
5. Si el owner pierde el derecho de invitar (por ejemplo, termina su ocupación), el otorgamiento se revoca en cascada.

El owner nunca elige puertas, solo destino y horario.

### 4.3 Visita sin aviso

- **Con guardia:** el guardia crea un **acceso temporal** (corto, de un solo uso o con vencimiento corto) desde su pantalla y le llega un aviso al owner del destino. Queda auditado.
- **Sin guardia:** el owner crea un **permiso temporal** desde la app: QR de un solo uso, sin documento, con vencimiento corto.

### 4.4 Invitado que no salió al vencer el permiso

No se alerta al guardia. Al vencer la validez, si la presencia indica que el sujeto sigue dentro, se genera una **Novedad** (`invitado_vencido_sin_salir`) en el módulo de administración, con estado `abierta` → `revisada` → `cerrada`.

### 4.5 Propietario que necesita entrar a la unidad alquilada

1. El propietario solicita un acceso temporal indicando motivo, día y horario.
2. El inquilino lo aprueba o rechaza desde la app.
3. Si aprueba, se crea un otorgamiento temporal a la puerta de la unidad con origen `invitacion`.

### 4.6 Cambio de inquilino

1. El administrador cierra la ocupación del inquilino saliente.
2. Se revocan en cascada sus otorgamientos e invitaciones.
3. El propietario recupera sus derechos de la plantilla `propietario_residente` (o `propietario_alquilado` si entra otro inquilino).
4. Se alta el nuevo inquilino como en 4.1.

## 5. Control de presencia

Las soluciones grandes fallan con el antipassback global porque alguien entra detrás de otro y el sistema se desincroniza. Por eso:

- La presencia **solo se mide en zonas** con lector de entrada y de salida. Donde no hay lector de salida, no se mide.
- **Modo suave por defecto:** se registra la violación y se genera un evento. El **modo duro** (bloquea) es opcional por zona.
- **Autolimpieza:** el estado de presencia caduca a las N horas o a una hora fija. Un olvido nunca deja a nadie bloqueado.
- **Offline:** cada dispositivo conoce solo su propia puerta; sin conexión, el antipassback global se degrada a suave. Es una decisión de diseño documentada.
- **Usos útiles:** "quién está dentro" para emergencias, límite de invitados simultáneos por unidad y aforo en espacios comunes. No se vende ni se expone como seguimiento de residentes.

## 6. Morosidad en espacios comunes

- Cada `Unidad` tiene `estado_financiero` (`al_dia`, `moroso`). En V1 lo carga el administrador; en el futuro puede integrarse con la administración del consorcio.
- Cada `Espacio` común tiene el flag `restringir_por_morosidad`.
- Si la unidad está en mora, el **compilador omite** los otorgamientos con origen `ocupacion` hacia los puntos que dan acceso a esos espacios restringidos. Al cambiar el estado se recompilan los dispositivos afectados.
- **Nunca se restringe** la puerta de la unidad ni el acceso principal al sitio. Esa regla es fija y no configurable.
- Las **invitaciones** del owner moroso a esos espacios quedan en suspenso mientras dure la mora.
- Todo cambio de estado financiero queda auditado.

## 7. Plantillas de acceso por rol

Una `plantilla_acceso_rol` (por sitio y rol de ocupación) apunta a un **perfil de acceso** y define los derechos: `puede_invitar`, `acceso_puerta_unidad`. Al crear un sitio se siembra un valor por defecto según su tipo (edificio, country, empresa o casa) y el administrador lo ajusta.

Valor por defecto en edificios:

| Rol de ocupación | Acceso al sitio y comunes | Puerta de la unidad | Puede invitar |
|---|---|---|---|
| Propietario residente | Sí | Sí | Sí |
| Inquilino | Sí | Sí | Sí |
| Propietario con inquilino | Sí | No | No |
