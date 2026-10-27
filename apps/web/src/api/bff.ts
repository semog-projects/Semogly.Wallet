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

/** Login de desenvolvimento: o Gateway obtém o token do Keycloak com usuário e senha (fluxo OIDC na #40). */
export async function login(username: string, password: string): Promise<SessionUser> {
  const response = await fetch('/bff/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', [CSRF_HEADER]: '1' },
    body: JSON.stringify({ username, password }),
  })

  if (!response.ok) {
    throw new ApiError(
      response.status,
      response.status === 401 ? 'Usuário ou senha inválidos.' : 'Não foi possível entrar. Tente novamente.',
    )
  }

  return (await response.json()) as SessionUser
}

export async function logout(): Promise<void> {
  await fetch('/bff/logout', { method: 'POST', headers: { [CSRF_HEADER]: '1' } })
}
