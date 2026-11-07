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

**Protocolo:** MQTT sobre TLS (Mosquitto o EMQX). El backend .NET se suscribe con MQTTnet. Alternativa más simple para el prototipo: HTTPS con WebSocket o long-polling.

Tópicos propuestos:

```
nexgate/{org}/{sitio}/{dispositivo}/state    (retenido) estado, firmware, batería, versión de política
nexgate/{org}/{sitio}/{dispositivo}/evt      eventos de acceso, alarmas
nexgate/{org}/{sitio}/{dispositivo}/cmd      comandos (abrir, sincronizar, reiniciar, ota)
nexgate/{org}/{sitio}/{dispositivo}/policy   aviso de nueva versión de política
```

- **Política:** el aviso llega por MQTT y el dispositivo descarga el blob (firmado) por HTTPS. Evita mandar listas grandes por MQTT.
- **Eventos:** se envían con id único (idempotencia) y se encolan localmente si no hay red.
- **Heartbeat:** cada pocos minutos, con versión de política, firmware y batería. El panel muestra **salud del dispositivo** y genera una Novedad si deja de reportar.

## Seguridad

- **Identidad por dispositivo:** certificado o token propio, nunca compartido. Se revoca individualmente.
- **Arranque seguro y flash cifrada** (secure boot v2 y flash encryption del ESP32).
- **OTA firmado**, con verificación de firma, versión mínima y **rollback automático** si el firmware nuevo no se reporta sano.
- **Credenciales hasheadas** en el dispositivo (HMAC con clave del dispositivo).
- **Comandos firmados** y con vencimiento, para que un mensaje viejo no se reutilice.
- **Detección de tamper** (apertura de caja) con evento inmediato.
- **Provisioning:** el instalador reclama el dispositivo con un código (QR en la caja) por BLE o portal cautivo, y lo asocia a un punto de acceso desde el panel.

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
