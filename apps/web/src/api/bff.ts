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

export async function login(username: string, roles: string[]): Promise<SessionUser> {
  const response = await fetch('/bff/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', [CSRF_HEADER]: '1' },
    body: JSON.stringify({ username, roles }),
  })

  if (!response.ok) {
    throw new ApiError(response.status, 'Não foi possível entrar. Verifique os dados e tente novamente.')
  }

  return (await response.json()) as SessionUser
}

export async function logout(): Promise<void> {
  await fetch('/bff/logout', { method: 'POST', headers: { [CSRF_HEADER]: '1' } })
}
