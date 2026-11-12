# Nexgate

Plataforma multitenant de control de acceso físico (edificios primero; countries, empresas y casas después).

## Dónde está cada cosa

- `docs/`: diseño de referencia. Empezar por `docs/01-decisiones.md` (decisiones con ID D-xx) y `docs/08-plan-de-cambios-openspec.md`.
- `openspec/config.yaml`: contexto y reglas del proyecto para los artefactos de OpenSpec.
- `openspec/specs/`: comportamiento acordado del sistema (fuente de verdad).
- `openspec/changes/`: cambios en curso, cada uno con proposal, specs, design y tasks.
- `src/`: capas numeradas al estilo SMT (D-32): `02.Api` (API con controllers y workers), `03.Service` (`Nexgate.Service.<Modulo>` y su `.Contracts`), `04.Data`, `05.Domain`, `06.Common`, `07.Test`. Un proyecto por módulo dentro de cada capa.

## Cómo trabajamos

Todo desarrollo pasa por OpenSpec, con la CLI `openspec`: explorar la idea, proponer el cambio (proposal, specs, design, tasks), implementarlo tarea por tarea y archivarlo. No se escribe código de producto sin un cambio con tareas aprobadas. Al proponer, usar el plan de `docs/08-plan-de-cambios-openspec.md`. Cada cambio debe pasar `openspec validate <cambio> --strict` y, al implementarlo, `dotnet build` y `dotnet test` en verde.

## Reglas que no se negocian

- Idioma: documentación e interfaz en español; código e identificadores en inglés. Los encabezados estructurales de OpenSpec y las palabras SHALL/MUST van en inglés.
- Todo dato de negocio lleva `organizacion_id` y RLS. Única excepción: el padrón del módulo People.
- Los datos de negocio solo referencian `SujetoId`, nunca el documento de una persona.
- Los módulos se referencian solo por sus `*.Contracts`.
- Un solo motor de permisos (Otorgamiento); gana siempre la denegación.
- La morosidad nunca restringe la puerta de la unidad ni el acceso principal al sitio.
- Nunca registrar datos personales en logs ni trazas.
