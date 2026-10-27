import { ApiError, CSRF_HEADER } from './client'

// Endpoints do BFF no próprio Gateway (fora do OpenAPI dos serviços)

export const OPERATOR_ROLE = 'operator'

export type SessionUser = {
  username: string
  roles: string[]
}

/** Usuário da sessão atual, ou null se não houver sessão. */
export async function getSession(): Promise<SessionUser | null> {
  const response = await fetch('/bff/user')

  if (response.status === 401) {
    return null
  }

  if (!response.ok) {
    throw new ApiError(response.status, 'Não foi possível verificar a sessão.')
  }

  return (await response.json()) as SessionUser
}

/**
 * Login OIDC: é uma navegação, não um fetch. O Gateway redireciona para a tela do Keycloak e, no retorno,
 * cria a sessão e volta para returnUrl (sempre um caminho local).
 */
export function loginUrl(returnUrl: string): string {
  return `/bff/login?${new URLSearchParams({ returnUrl })}`
}

/** Encerra a sessão do web e a do Keycloak (o Gateway faz o logout federado pelo canal de servidor). */
export async function logout(): Promise<void> {
  await fetch('/bff/logout', { method: 'POST', headers: { [CSRF_HEADER]: '1' } })
}
