# Vectores de prueba de la decisión de acceso

Casos de prueba de la decisión de acceso (D-13), compartidos entre el backend y el firmware. Cada archivo de `v1/` es un caso independiente: una política compilada en formato v1, una credencial presentada, un instante y la decisión esperada. No dependen del backend, así que el firmware puede ejecutar los mismos archivos.

Las pruebas del backend (`AccessVectorsTests`) recorren todos los archivos de `v1/` y fallan si alguno no da el resultado esperado o si la carpeta está vacía.

## Formato de un vector

```json
{
  "description": "Qué se prueba, en español",
  "policy": { "format": 1, "...": "política compilada v1" },
  "credentialHash": "HMAC de la credencial presentada (base64url)",
  "at": "2026-11-24T13:00:00Z",
  "expected": { "decision": "allow | deny", "reason": "allowed | credential_invalid | explicit_deny | no_permission" }
}
```

## Política compilada v1

| Campo | Contenido |
|---|---|
| `format` | `1` |
| `siteId`, `accessPointId` | Identificadores del sitio y del punto de acceso |
| `timeZone` | Zona horaria IANA del sitio (informativa: la hora local sale de `offsets`) |
| `offsets` | Lista de `{ from, seconds }`: desplazamiento UTC en segundos vigente desde `from` (segundos Unix; `null` es desde siempre). Aplica la última fila con `from` nulo o menor o igual que el instante |
| `windows` | Lista de horarios; cada horario es una lista de `{ days, from, to }`. `days` es una máscara (bit 0 = lunes … bit 6 = domingo); `from` y `to`, minutos del día locales con intervalo `[from, to)` |
| `credentials` | Lista de `{ hash, from, to }`: credenciales activas con su vigencia `[from, to)` en segundos Unix (`null` es sin límite) |
| `entries` | Lista de `{ credential, window, from, to, uses, deny }`: índice de la credencial, índice del horario (`null` es siempre), vigencia `[from, to)`, usos restantes (`null` es sin límite) y si deniega |

## Algoritmo

1. Si la credencial no está en `credentials` o el instante cae fuera de su vigencia: `deny` / `credential_invalid`.
2. Calcular el día de la semana y el minuto del día locales con `offsets`.
3. Tomar las entradas de esa credencial que aplican: el instante está dentro de su vigencia y, si tienen horario, alguna ventana contiene el día y el minuto locales.
4. Si alguna que aplica deniega: `deny` / `explicit_deny` (gana siempre la denegación).
5. Si alguna que aplica permite y tiene usos restantes (o no tiene límite): `allow` / `allowed`.
6. Si no: `deny` / `no_permission`.

## Agregar un vector

Un archivo nuevo en `v1/` con un nombre que empiece con el número siguiente y describa el caso. Si el formato de la política cambia de forma incompatible, se crea `v2/` y `v1/` se conserva mientras haya firmware que lo use.
