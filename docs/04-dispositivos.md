# 04 Dispositivos

## Objetivo

Un controlador de puerta barato, que **decide solo** (offline), se **actualiza solo** (WiFi) y **reporta todo**. Debe poder convivir con los controles RF 433 MHz que ya usan los edificios de Córdoba y con credenciales más seguras.

## Hardware de referencia (prototipo)

| Función | Componente |
|---|---|
| Microcontrolador | ESP32-S3 (o C3 si no hace falta BLE avanzado ni cámara) |
| Salida de puerta | Relé o driver MOSFET para cerradura/barrera, con diodo de protección |
| Entrada RF | Receptor 433 MHz para los controles existentes |
| Entrada lector | Wiegand (lectores RFID/NFC comerciales económicos) |
| Entrada QR | Lector QR por UART (o módulo con cámara) |
| Entradas | Pulsador de salida, sensor de puerta, tamper |
| Reloj | RTC con batería (DS3231 o similar) para evaluar horarios sin red |
| Alimentación | Fuente + respaldo con batería. Medición de batería |
| Feedback | LED y buzzer |

> Ventaja del enfoque: el firmware es una caja con **entradas de credencial intercambiables** (RF, Wiegand, QR, BLE) y una sola lógica de decisión. Agregar un tipo de lector nuevo no cambia el núcleo.

## Firmware

- **Producto final:** firmware propio (ESP-IDF o Arduino/PlatformIO) para controlar OTA, seguridad y lógica offline.
- **Prototipos:** ESPHome es válido para probar rápido. Se descarta para producción.
- **Almacenamiento local:** política compilada y cola de eventos en flash (LittleFS), con anillo circular para los eventos.
- **Decisión:** el mismo algoritmo de evaluación del documento 03, con la lista de credenciales hasheadas.
- **Tiempo:** NTP al conectar, RTC como respaldo. Se registra la deriva y se alerta si el reloj es poco confiable.

## Comunicación

**Prototipo (implementado en `add-device-sync-protocol`): HTTPS con sondeo**, en un endpoint de Kestrel aparte que acepta certificados de cliente (`Kestrel:Endpoints:Devices`, puerto 7443 en desarrollo):

| Operación | Ruta | Autenticación |
|---|---|---|
| Reclamo | `POST /api/v1/device/claim` | código de reclamo (con límite de intentos) |
| Política | `GET /api/v1/device/policy` | certificado; `If-None-Match` con la versión, firma en `X-Nexgate-Signature` |
| Latido | `POST /api/v1/device/heartbeat` | certificado; responde versión vigente, hora del servidor y cada cuántos segundos volver |
| Eventos | `POST /api/v1/device/events` | certificado; lotes de hasta 100, idempotentes por id |

**Destino: MQTT sobre TLS** (Mosquitto o EMQX) para avisos inmediatos y comandos. El backend .NET se suscribe con MQTTnet.

Tópicos propuestos:

```
nexgate/{org}/{sitio}/{dispositivo}/state    (retenido) estado, firmware, batería, versión de política
nexgate/{org}/{sitio}/{dispositivo}/evt      eventos de acceso, alarmas
nexgate/{org}/{sitio}/{dispositivo}/cmd      comandos (abrir, sincronizar, reiniciar, ota)
nexgate/{org}/{sitio}/{dispositivo}/policy   aviso de nueva versión de política
```

- **Política:** con MQTT el aviso llegará por el tópico `policy`; la descarga del paquete firmado sigue siendo por HTTPS. Evita mandar listas grandes por MQTT.
- **Eventos:** se envían con id único (idempotencia) y se encolan localmente si no hay red.
- **Heartbeat:** cada `pollSeconds` (60 por defecto), con versión de política y firmware (la batería llega con salud). El panel muestra el último contacto; la **salud del dispositivo** y la Novedad por falta de reporte llegan con `add-device-ota-and-health`.

## Seguridad

- **Identidad por dispositivo (D-35):** certificado de cliente emitido por la autoridad de dispositivos de Nexgate al reclamarlo. El dispositivo genera su clave P-256 y manda una solicitud de certificado; la clave privada nunca sale de él. Se revoca individualmente (o se invalida al emitir un código nuevo) y el backend lo rechaza en la solicitud siguiente.
- **Arranque seguro y flash cifrada** (secure boot v2 y flash encryption del ESP32).
- **OTA firmado**, con verificación de firma, versión mínima y **rollback automático** si el firmware nuevo no se reporta sano.
- **Credenciales hasheadas** en el dispositivo (HMAC con la clave del sitio, que recibe al reclamarlo; `docs/03`).
- **Política firmada** (ECDSA P-256): el dispositivo verifica la firma antes de reemplazar la política vigente y la vuelve a verificar al reiniciar (`vectors/device-policy/`).
- **Comandos firmados** y con vencimiento, para que un mensaje viejo no se reutilice.
- **Detección de tamper** (apertura de caja) con evento inmediato.
- **Provisioning:** el administrador da de alta el dispositivo en el panel y obtiene un código de reclamo (un solo uso, vence a los 7 días). El instalador lo carga en el dispositivo (por BLE o portal cautivo), que lo canjea por su certificado. Los puntos que controla se asignan desde el panel.

## Seguridad física y de egreso

- La puerta **siempre abre desde adentro** (pulsador de salida, barra antipánico o rotura de vidrio según el caso).
- Cada punto de acceso define su **modo de fallo**:
  - **Fail-safe** (se abre sin energía): salidas de emergencia y puertas de evacuación.
  - **Fail-secure** (queda cerrada sin energía): zonas que deben protegerse, siempre con salida mecánica.
- **Batería de respaldo** y alerta de batería baja.
- **Verificar la normativa local de incendio y evacuación** antes de instalar en un edificio real. Es un punto de validación con un profesional.

## Offline-first

| Situación | Comportamiento |
|---|---|
| Sin internet | Decide con la política local y el RTC. Encola eventos. |
| Política vieja | Sigue funcionando con la última versión válida. Alerta en el panel por desactualización. |
| Reinicio | Recupera política y cola desde flash. |
| Reloj dudoso | Usa un modo conservador (se configura por punto: permitir o denegar fuera de sincronía). |
| Presencia | Antipassback global se degrada a suave. |

## Retrofit: convivir con los controles RF existentes

Hoy cada edificio tiene un dispositivo RF con códigos cargados a mano. Para migrar sin fricción:

1. Instalar un dispositivo Nexgate **en paralelo** (o reemplazando el receptor) con receptor 433 MHz.
2. **Importar los códigos existentes** (modo aprendizaje: el administrador acciona cada control y lo asocia a una persona).
3. Operar un tiempo con ambos y mostrar el valor de la auditoría y de la gestión remota.
4. Ofrecer el cambio de controles por credenciales más seguras.

**Advertencia de seguridad:** la mayoría de los controles de código fijo se pueden copiar con un receptor barato. Hay que comunicarlo a los clientes como el motivo para migrar a BLE, NFC o QR dinámico, y no ocultarlo.

## Pruebas de campo (checklist del piloto)

- Apertura con y sin internet, con corte de luz y con la puerta sin energía.
- Política nueva entrando mientras la puerta se usa.
- Reinicios sucesivos, flash llena de eventos, reloj sin NTP.
- OTA exitoso y OTA fallido con rollback.
- Interferencia RF en el entorno (por ejemplo cerca de antenas celulares).
- Tiempo de respuesta lector→relé.
