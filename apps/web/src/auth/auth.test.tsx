import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { redirectTo } from '@/lib/navigation'
import { mockFetch, renderApp } from '@/test/render'

// jsdom não navega: o redirecionamento para o Keycloak é verificado pela chamada
vi.mock('@/lib/navigation', () => ({ redirectTo: vi.fn() }))

const book = { cashIn: -500, clearing: 0, customerBalances: 500, customerAccounts: 2, total: 0, isBalanced: true }

describe('autenticação do backoffice', () => {
  beforeEach(() => vi.mocked(redirectTo).mockClear())

  it('sem sessão, redireciona para o login', async () => {
    mockFetch([{ path: '/bff/user', status: 401 }])

    renderApp('/')

    expect(await screen.findByRole('button', { name: 'Entrar com Keycloak' })).toBeInTheDocument()
  })

  it('entrar leva ao login OIDC do Gateway, voltando para a rota protegida de origem', async () => {
    mockFetch([{ path: '/bff/user', status: 401 }])

    renderApp('/transfers')
    await userEvent.click(await screen.findByRole('button', { name: 'Entrar com Keycloak' }))

    expect(redirectTo).toHaveBeenCalledWith('/bff/login?returnUrl=%2Ftransfers')
  })

  it('com sessão de operador, a tela de login segue para a visão geral', async () => {
    mockFetch([
      { path: '/bff/user', body: { username: 'backoffice@semogly.dev', roles: ['operator'] } },
      { path: '/api/ledger/book', body: book },
    ])

    renderApp('/login')

    expect(await screen.findByText('Livro balanceado')).toBeInTheDocument()
    expect(redirectTo).not.toHaveBeenCalled()
  })

  it('usuário sem o papel operator vê acesso restrito', async () => {
    mockFetch([{ path: '/bff/user', body: { username: 'ana', roles: [] } }])

    renderApp('/')

    expect(await screen.findByText('Acesso restrito')).toBeInTheDocument()
  })

  it('chamadas à API enviam o header anti-CSRF', async () => {
    const fetchMock = mockFetch([
      { path: '/bff/user', body: { username: 'backoffice', roles: ['operator'] } },
      { path: '/api/ledger/book', body: book },
    ])

    renderApp('/')

    await waitFor(() => expect(screen.getByText('Livro balanceado')).toBeInTheDocument())
    const apiRequest = fetchMock.mock.calls
      .map(([input]) => input)
      .find((input): input is Request => input instanceof Request && input.url.endsWith('/api/ledger/book'))
    expect(apiRequest?.headers.get('X-CSRF')).toBe('1')
  })
})
