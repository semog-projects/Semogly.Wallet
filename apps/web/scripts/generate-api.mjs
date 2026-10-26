// Gera os tipos TypeScript dos serviços a partir do OpenAPI publicado pelo Gateway.
// Uso: com o AppHost rodando, `npm run api:generate` (GATEWAY_URL sobrescreve o endereço padrão).
import { execSync } from 'node:child_process'

const gateway = process.env.GATEWAY_URL ?? 'https://localhost:7100'
const services = ['accounts', 'transfers', 'ledger', 'fraud', 'notifications']

// O Gateway usa o certificado de desenvolvimento do .NET
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'

for (const service of services) {
  // openapi-typescript roda isolado via npx: exige TypeScript 5 e o projeto usa o 6
  execSync(`npx --yes openapi-typescript@7 ${gateway}/openapi/${service}.json -o src/api/schema/${service}.ts`, {
    stdio: 'inherit',
  })
}
