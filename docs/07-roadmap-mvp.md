# 07 Roadmap y MVP

## Fases

### Fase 0: Definición y validación (ahora)

- Documentos de arquitectura (esta carpeta).
- Conseguir un **edificio piloto** y relevar puertas, cerraduras, energía y WiFi.
- Prototipo de hardware sobre mesa: ESP32 + relé + RF + lector Wiegand.
- Definir forma jurídica y empezar el trámite de registro.

**Salida:** edificio piloto confirmado, hardware de prueba abriendo un relé desde la nube y checklist legal en marcha.

### Fase 1: Prototipo técnico

- Backend mínimo: Tenancy, Identity, Spaces, Devices, Events.
- Un dispositivo conectado por MQTT, con política compilada simple y eventos.
- Panel mínimo: alta de sitio, espacios, puertas, credenciales y ver eventos.
- Pruebas de corte de internet y de energía.

**Salida:** una puerta real que abre por credencial gestionada desde la nube y sigue funcionando sin internet.

### Fase 2: MVP edificios (piloto)

- Módulos completos de **Occupancy** y **Access**: unidades, ocupaciones, membresías, plantillas, perfiles, horarios y otorgamientos.
- Alta de residentes por el administrador.
- **Invitaciones** nominales, recurrentes y temporales, con libreta de contactos y QR firmado.
- Rol guardia: accesos temporales y eventos en vivo.
- Propietario con inquilino y plantilla configurable.
- **Padrón único** con las protecciones definidas.
- Novedades y presencia en modo suave.
- OTA firmado y salud de dispositivos.
- Retención y anonimización automática.

**Salida:** un edificio completo operando en producción durante varias semanas sin intervención manual sobre los dispositivos.

### Fase 3: Madurez y expansión

- Presencia en modo duro, aforo y cupos en espacios comunes.
- Morosidad (con integración con la administración del consorcio).
- BLE (llave en el celular), notificaciones por WhatsApp.
- Primer **country** y primera **empresa** (turnos, horarios, áreas), validando que el modelo no requiere cambios de núcleo.
- Facturación y planes.

## Alcance del MVP

| Entra | No entra todavía |
|---|---|
| Edificios con múltiples puertas y zonas | Biometría |
| Residentes, invitados, empleada doméstica | Control de pisos en ascensor |
| Guardia con accesos temporales | Integración con alarmas y video |
| Propietario / inquilino | Reglas de acompañante o dos personas |
| Presencia en modo suave | Muster (conteo en evacuación) |
| Retrofit RF 433 MHz | Integración financiera automática |
| OTA y salud de dispositivos | App nativa |

## Riesgos principales

| Riesgo | Mitigación |
|---|---|
| Hardware: soporte, instalación y garantía son un negocio en sí | Definir modelo (venta, alquiler o abono) y partner de instalación |
| Seguridad de egreso e incendio | Validar con profesional; modo de fallo por puerta |
| Padrón compartido y Ley 25.326 | Diseño de privacidad y revisión legal antes del piloto |
| Controles RF de código fijo copiables | Comunicar y ofrecer migración a credenciales seguras |
| Dependencia de internet del edificio | Offline-first con política local |
| Alcance que crece | Mantener el MVP acotado y extensiones fuera del núcleo |

## Próximos pasos inmediatos

1. Confirmar nombre del proyecto y forma jurídica.
2. Conseguir el edificio piloto y relevar las puertas.
3. Armar el prototipo de hardware sobre mesa.
4. ~~Crear la solución .NET con el esqueleto de módulos y `docker-compose` (PostgreSQL + Mosquitto).~~ Hecho en `add-solution-foundation`.
5. Implementar primero el **evaluador de acceso** y sus vectores de prueba compartidos con el firmware.
6. Revisar el documento legal con un abogado.
