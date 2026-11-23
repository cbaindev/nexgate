# Vectores de verificación de la política del dispositivo

Casos de prueba de la firma de los paquetes de política (`add-device-sync-protocol`), compartidos entre el backend y el firmware. Cada archivo de `v1/` es un caso independiente: una clave pública, los bytes exactos de un paquete, una firma y si la verificación tiene que dar válida. Los casos inválidos son paquetes alterados después de firmar, firmas de otra clave y firmas truncadas: el firmware tiene que rechazarlos y seguir con la última política que verificó.

Las pruebas del backend (`DevicePolicyVectorsTests`) recorren todos los archivos de `v1/`, verifican cada uno con la misma rutina que usa el servidor y fallan si alguno no da el resultado esperado o si la carpeta está vacía. La clave de estos vectores es solo para vectores: no es la de desarrollo ni la de producción.

## Cómo llega el paquete

`GET /api/v1/device/policy` con el certificado de cliente del dispositivo:

- El cuerpo es el paquete en JSON, tal como se firmó. El dispositivo guarda **esos bytes**, sin volver a serializarlos.
- El encabezado `X-Nexgate-Signature` trae la firma ECDSA P-256 con SHA-256 de esos bytes, en formato IEEE P1363 (`r || s`, 64 bytes) y base64url sin relleno.
- El encabezado `ETag` trae la versión entre comillas (`"7"`). Con `If-None-Match: "7"` y la versión vigente, la respuesta es `304` sin cuerpo.
- La clave pública para verificar (PEM, `SubjectPublicKeyInfo`) llega en la respuesta del reclamo, campo `policyKey`.

## Formato de un vector

```json
{
  "description": "Qué se prueba, en español",
  "publicKey": "-----BEGIN PUBLIC KEY-----\n…",
  "package": "bytes exactos del paquete, en base64",
  "signature": "firma IEEE P1363 en base64url",
  "valid": true
}
```

## Paquete de política v1

| Campo | Contenido |
|---|---|
| `format` | `1` |
| `deviceId`, `siteId` | Identificadores del dispositivo y de su sitio |
| `version` | Versión monotónica de la política del dispositivo: empieza en 1 y avanza de a uno cuando cambia el contenido |
| `issuedAt` | Instante en que se emitió esa versión (UTC) |
| `points` | Política v1 de cada punto que controla el dispositivo, ordenadas por `accessPointId` (formato en `vectors/access-evaluation/README.md`) |

## Qué hace el firmware

1. Al recibir un paquete, verifica la firma sobre los bytes recibidos con la clave del reclamo.
2. Si es válida, guarda los bytes y la firma en flash, y recién entonces reemplaza la política vigente.
3. Si no es válida, la descarta y sigue con la última política verificada (funcionamiento sin conexión).
4. Al reiniciar, vuelve a verificar la política guardada antes de usarla.
