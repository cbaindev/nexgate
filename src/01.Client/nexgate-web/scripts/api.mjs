// Tipos de la API generados desde el contrato versionado (D-34, design de add-admin-panel-shell, decisión 2).
//   node scripts/api.mjs generate  → escribe src/api/schema.d.ts
//   node scripts/api.mjs check     → falla si src/api/schema.d.ts no coincide con el contrato
import { readFile, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import openapiTS, { astToString } from 'openapi-typescript'

const contract = new URL('../../../../api/nexgate-v1.openapi.json', import.meta.url)
const output = new URL('../src/api/schema.d.ts', import.meta.url)
const header =
  '// Generado por `npm run api:generate` desde api/nexgate-v1.openapi.json. No editar a mano.\n' +
  '/* eslint-disable */\n'

async function generate() {
  const document = JSON.parse(await readFile(contract, 'utf8'))
  const ast = await openapiTS(document, { alphabetize: true })
  return header + astToString(ast).replace(/\r\n/g, '\n')
}

const mode = process.argv[2]
const generated = await generate()

if (mode === 'generate') {
  await writeFile(output, generated)
  console.log(`Tipos generados en ${fileURLToPath(output)}`)
} else if (mode === 'check') {
  let current = ''
  try {
    current = (await readFile(output, 'utf8')).replace(/\r\n/g, '\n')
  } catch {
    // Sin archivo: no coincide.
  }

  if (current !== generated) {
    console.error('Los tipos de la API no coinciden con api/nexgate-v1.openapi.json. Regenerarlos con: npm run api:generate')
    process.exit(1)
  }

  console.log('Los tipos de la API coinciden con el contrato.')
} else {
  console.error('Uso: node scripts/api.mjs generate|check')
  process.exit(2)
}
