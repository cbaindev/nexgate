# 01 Decisiones

Registro de decisiones tomadas durante la fase de definición (2026-11-06). Cada decisión tiene un ID para referenciarla desde el código y los demás documentos.

## Decisiones tomadas

### Alcance y estructura

| ID | Decisión |
|---|---|
| D-01 | Primer alcance: edificios de Córdoba. El modelo es genérico para countries, barrios cerrados, empresas y casas. |
| D-02 | Dos niveles de multitenancy: **Organización** (cuenta que contrata y es el límite de aislamiento y facturación) → **Sitio** (edificio, country, empresa o casa). |
| D-03 | Los roles se asignan por **alcance**: Usuario + Rol + Alcance (organización, sitio o espacio). |
| D-04 | Un usuario puede tener membresía en varias organizaciones (selector de organización al ingresar). La delegación entre organizaciones queda para después. |
| D-05 | Una sola aplicación y un solo panel con vistas según rol: administrador, guardia, residente o empleado. |

### Personas, unidades y espacios

| ID | Decisión |
|---|---|
| D-06 | Tres conceptos separados: **Persona** (identidad), **Unidad** (familia, empresa, lote) y **Espacio** (nodo del árbol físico). Se relacionan con **Membresía** (Persona↔Unidad) y **Ocupación** (Unidad↔Espacio), ambas con rol y vigencia. |
| D-07 | Los residentes y empleados los da de alta el **administrador**. Los invitados los da de alta el **owner** del espacio. |
| D-08 | Una persona puede ser owner de más de un espacio y puede invitar a cada uno por separado. |
| D-09 | Los espacios comunes son espacios como cualquier otro: tienen owners que pueden invitar. Sin cupo por ahora (el campo queda previsto). |

### Alquiler y propietarios

| ID | Decisión |
|---|---|
| D-10 | Con el espacio alquilado, el que invita es el **inquilino**. |
| D-11 | El propietario conserva acceso al sitio según una **plantilla de acceso por rol de ocupación**, configurable por sitio con un valor por defecto: acceso al sitio y zonas comunes, sin la puerta de la unidad y sin derecho a invitar mientras haya inquilino activo. |
| D-12 | Si el propietario necesita entrar a la unidad alquilada, pide un acceso temporal que el inquilino aprueba desde la app. |

### Permisos, invitados y presencia

| ID | Decisión |
|---|---|
| D-13 | Todo permiso es un **Otorgamiento**: Sujeto → Destino → Horario → Vigencia → Origen. Un solo motor de evaluación. Gana siempre la denegación. |
| D-14 | Tipos de invitación: **nominal**, **recurrente** (empleada doméstica, con días y horarios), **temporal** (QR anónimo) y **temporal de guardia**. |
| D-15 | Al invitar, el owner elige solo destino y horario. Las puertas del camino se derivan automáticamente. |
| D-16 | Visita sin aviso: si hay guardia, este crea un acceso temporal y avisa al owner. Si no hay guardia, el owner crea un permiso temporal desde la app (QR de un solo uso sin documento). |
| D-17 | Control de presencia solo en zonas con lector de entrada y de salida. Modo suave por defecto, modo duro opcional por zona. El estado de presencia caduca solo. Sin conexión, se degrada a suave. |
| D-18 | Si un invitado no salió al vencer su permiso, **no se alerta al guardia**: se genera una **Novedad** en el módulo de administración, con estado y trazabilidad. |
| D-19 | La morosidad puede restringir el acceso a **espacios comunes** (opción por zona). Nunca restringe la puerta de la unidad ni el acceso principal al sitio. En V1 el estado lo carga el administrador a mano. |
| D-20 | La facturación se define más adelante. |

### Privacidad

| ID | Decisión |
|---|---|
| D-21 | **Padrón único** de personas por documento o pasaporte para toda la plataforma. La clave es tipo de documento + país emisor + número, con hash HMAC para búsqueda y dato real cifrado. |
| D-22 | Cada tenant ve solo **Sujetos** locales. La relación con el padrón es opaca. Nadie consulta el padrón: el owner carga el nombre que conoce y el sistema vincula en silencio, sin devolver datos de la persona existente. |
| D-23 | Cada owner tiene su **libreta de contactos frecuentes** (alias, teléfono, plantilla de horarios). |
| D-24 | El administrador ve las unidades pero **no los datos personales de los invitados**. Los eventos de invitados se muestran con identificador seudónimo. |
| D-25 | Los invitados temporales (delivery, service) son anónimos y no entran al padrón. Los menores o personas sin documento se cargan como "sin documento" y tampoco entran al padrón global. |
| D-26 | Retención configurable por organización, con un valor por defecto corto para invitados y anonimización automática. |

### Tecnología

| ID | Decisión |
|---|---|
| D-27 | Backend **.NET** como **monolito modular** con un esquema de PostgreSQL por módulo, una sola base de datos y comunicación entre módulos por contratos y eventos (outbox). |
| D-28 | Base de datos única **PostgreSQL** con Row-Level Security por tenant. |
| D-29 | Frontend **React + TypeScript**, panel único y PWA para residentes. |
| D-30 | Dispositivos **ESP32** con firmware propio. ESPHome solo para prototipos. Comunicación MQTT sobre TLS. |
| D-31 | Retrofit: aceptar los controles RF 433 MHz existentes para migrar gradualmente, empujando después a credenciales seguras (BLE, NFC, QR dinámico). |
| D-32 | El contrato entre el frontend y el backend es el **documento OpenAPI** de la API, versionado en el repositorio y verificado por las pruebas. El frontend se comunica a través de un cliente o proxy generado desde ese documento. |

## Preguntas abiertas

| ID | Pregunta | Propuesta |
|---|---|---|
| Q-01 | ¿Retención exacta de eventos e invitados? | Configurable por organización. Defaults a definir con el abogado. |
| Q-02 | ¿Modelo de cobro (por puerta, unidad o sitio)? | Diferido. Contar entidades desde el día uno para poder decidir con datos. |
| Q-03 | ¿Forma jurídica del producto (SAS) y responsable del padrón? | Definir antes del piloto. |
| Q-04 | ¿Idioma del código? | **Resuelta** (aplicada en `add-solution-foundation`): identificadores y código en inglés, interfaz y documentación en español, i18n desde el inicio. |
| Q-05 | ¿Edificio piloto y cantidad de puertas? | Conseguir uno cercano para validar hardware y flujos. |
| Q-06 | ¿Hardware: fabricar placa propia o módulos comerciales? | Prototipo con módulos, placa propia después del piloto. |
| Q-07 | ¿Integración con administración de consorcios para morosidad? | Fuera del MVP; carga manual primero. |
