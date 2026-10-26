import createClient, { type Middleware } from 'openapi-fetch'
import type { paths as AccountsPaths } from './schema/accounts'
import type { paths as FraudPaths } from './schema/fraud'
import type { paths as LedgerPaths } from './schema/ledger'
import type { paths as NotificationsPaths } from './schema/notifications'
import type { paths as TransfersPaths } from './schema/transfers'

export const CSRF_HEADER = 'X-CSRF'

/** Erro HTTP da API, com o status para tratar 401/403 na interface. */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// A sessão é o cookie HttpOnly do BFF (enviado automaticamente na mesma origem);
// o header anti-CSRF é exigido pelo Gateway em toda requisição que altera estado
const csrf: Middleware = {
  onRequest({ request }) {
    request.headers.set(CSRF_HEADER, '1')
    return request
  },
}

function create<Paths extends object>() {
  // Mesma origem da página: o proxy do Vite (dev) ou o mesmo host (produção) leva ao Gateway.
  // URL absoluta porque o Request do Node (testes) não aceita caminhos relativos
  const client = createClient<Paths>({
    baseUrl: `${window.location.origin}/api`,
    // Resolve o fetch a cada chamada (o padrão captura a referência na criação, o que impede mocks)
    fetch: (request) => globalThis.fetch(request),
  })
  client.use(csrf)
  return client
}

/** Clientes tipados gerados a partir do OpenAPI de cada serviço (npm run api:generate). */
export const api = {
  accounts: create<AccountsPaths>(),
  transfers: create<TransfersPaths>(),
  ledger: create<LedgerPaths>(),
  fraud: create<FraudPaths>(),
  notifications: create<NotificationsPaths>(),
}

/** Extrai o dado ou lança ApiError, para uso direto nas queries do TanStack Query. */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.data !== undefined) {
    return result.data
  }

  const detail = (result.error as { detail?: string; title?: string } | undefined) ?? {}
  throw new ApiError(result.response.status, detail.detail ?? detail.title ?? `Erro ${result.response.status}`)
}
