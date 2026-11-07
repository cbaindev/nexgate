# 06 Privacidad y legal

> Este documento es una guía de diseño, **no asesoramiento legal**. Conviene validarlo con un abogado especializado en protección de datos antes del piloto, en especial el padrón compartido.

## Marco normativo

- Rige la **Ley 25.326** de Protección de Datos Personales y su reglamentación. La autoridad de aplicación es la **AAIP**.
- Existe un proyecto de reforma impulsado por la AAIP que todavía espera debate en el Congreso (verificado en noviembre de 2026). Por eso el diseño apunta a mejores prácticas y no solo al mínimo legal actual.

## Obligaciones formales

- **Inscripción en el Registro Nacional de Bases de Datos Personales.** Primero se inscribe al **responsable** y después cada base. El trámite se hace por Trámites a Distancia (TAD) con clave fiscal nivel 2 o superior. La inscripción no vence, pero se debe actualizar si cambian los datos declarados.
- Aunque no inscribir una base está clasificado como infracción leve, la obligación es exigible.
- **Forma jurídica:** constituir una sociedad para el producto antes del piloto, para que el responsable sea esa entidad y no una persona física.

## Roles

| Dato | Responsable | Encargado |
|---|---|---|
| Padrón global de identidad | La empresa de Nexgate | |
| Datos de cada organización (unidades, accesos, eventos) | La organización cliente | La empresa de Nexgate |

La relación con cada organización cliente se formaliza con un **contrato de encargado de tratamiento** (confidencialidad, seguridad, subencargados, destino de los datos al terminar).

## Principios aplicados al producto

| Principio | Cómo lo cumple Nexgate |
|---|---|
| **Finalidad** | Solo control de acceso y seguridad. Sin marketing ni cesión a terceros. Se declara en el registro. |
| **Minimización** | Invitados temporales sin documento. Menores o personas sin documento fuera del padrón. |
| **Consentimiento informado** | Aviso claro al alta (residentes y empleados) y al recibir el QR o link (invitados), con enlace a la política de privacidad. |
| **Seguridad** | Cifrado del documento y datos sensibles, HMAC con pepper para búsquedas, RLS, auditoría de consultas a datos personales, backups cifrados. |
| **Confidencialidad** | Acceso por rol. El administrador no ve datos personales de invitados. |
| **Conservación limitada** | Retención configurable por organización, con anonimización o borrado automático. |
| **Datos sensibles** | No se tratan biometría ni datos de salud en esta etapa. Si se agrega biometría, requiere análisis aparte. |

## Padrón único: diseño seguro

- **Clave:** tipo de documento + país emisor + número, normalizados.
- **Búsqueda:** HMAC con pepper (el pepper vive fuera de la base de datos). El valor real se guarda **cifrado**.
- **Aislamiento:** los tenants nunca consultan el padrón. Trabajan con **Sujetos** locales; solo el módulo People conoce la relación Sujeto↔Persona.
- **Anti-enumeración:** el owner carga el nombre que él conoce. El sistema vincula en silencio y **no devuelve datos** de la persona si ya existía. El nombre verificado se muestra solo al guardia, en la puerta, con finalidad acotada.
- **Fusión de duplicados** (por ejemplo, DNI y pasaporte de la misma persona): acción manual de la plataforma, nunca automática.
- **Un tenant nunca ve** en qué otras organizaciones está una persona.

## Derechos de los titulares

- **Acceso:** la persona puede pedir qué datos se tienen y a quién se mostraron.
- **Rectificación y supresión:** se atiende por un canal de contacto visible (mail de privacidad) y con plazo definido.
- **Supresión y eventos:** como los eventos de acceso pueden tener valor de seguridad, la supresión se resuelve **anonimizando** (se pierde el vínculo con la persona, no el evento).
- **Baja del padrón:** cuando una persona deja de tener vínculos activos con cualquier organización, el dato se anonimiza pasado un período definido.

## Retención (valores a definir con el abogado)

| Dato | Propuesta inicial |
|---|---|
| Eventos de residentes y empleados | Configurable por organización |
| Eventos de invitados | Corto (por ejemplo, 30 a 90 días), luego anonimización |
| Datos del invitado en la libreta | Mientras el owner lo mantenga; se borran al cerrar la ocupación |
| Padrón sin vínculos activos | Anonimización tras período definido |

## Transferencias internacionales

Si la base o los backups están en nubes fuera de Argentina, hay que contemplar el régimen de **transferencia internacional de datos**. Para la primera etapa conviene elegir regiones y proveedores con eso resuelto o evaluarlo con el abogado.

## Incidentes

Definir un procedimiento antes del piloto: detección, contención, evaluación, notificación a la organización cliente y, si corresponde, a las personas afectadas y a la autoridad.

## Checklist antes del piloto

- [ ] Sociedad constituida y responsable definido
- [ ] Inscripción del responsable y de la base ante el Registro Nacional
- [ ] Contrato de encargado de tratamiento con el edificio piloto
- [ ] Aviso de privacidad y política de privacidad publicados
- [ ] Reglamento del edificio actualizado (uso de datos y control de acceso), con el administrador
- [ ] Canal de derechos de los titulares operativo
- [ ] Política de retención configurada
- [ ] Procedimiento de incidentes
- [ ] Revisión del padrón compartido con abogado

## Fuentes consultadas

- Obligaciones de los responsables de bases de datos personales: https://www.argentina.gob.ar/node/53779
- Trámites ante el Registro Nacional de Bases de Datos Personales: https://www.argentina.gob.ar/aaip/datospersonales/tramites
- Inscripción de responsables de bases privadas: https://www.argentina.gob.ar/servicio/inscribir-un-responsable-de-bases-de-datos-personales-privadas
- Vigencia y modificación de la inscripción: https://www.argentina.gob.ar/node/94798
- Reforma en debate (Diario Judicial): https://www.diariojudicial.com/news-99786-datos-personales-libro-y-debate-en-agenda
