import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderApp } from '@/test/render'

const book = { cashIn: -500, clearing: 0, customerBalances: 500, customerAccounts: 2, total: 0, isBalanced: true }

describe('autenticação do backoffice', () => {
  it('sem sessão, redireciona para o login', async () => {
    mockFetch([{ path: '/bff/user', status: 401 }])

    renderApp('/')

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument()
  })

  it('senha incorreta mostra a mensagem do Keycloak', async () => {
    mockFetch([
      { path: '/bff/user', status: 401 },
      { method: 'POST', path: '/bff/login', status: 401 },
    ])

    renderApp('/login')
    await userEvent.type(await screen.findByLabelText('Senha'), 'errada')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Usuário ou senha inválidos.')
  })

  it('login cria a sessão com o header anti-CSRF e abre a visão geral', async () => {
    const fetchMock = mockFetch([
      { path: '/bff/user', status: 401 },
      { method: 'POST', path: '/bff/login', body: { username: 'backoffice', roles: ['operator'] } },
      { path: '/api/ledger/book', body: book },
    ])

    renderApp('/login')
    await userEvent.type(await screen.findByLabelText('Senha'), 'Senha@123')
    await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Livro balanceado')).toBeInTheDocument()

    const [, init] = fetchMock.mock.calls.find(([input]) => String(input) === '/bff/login')!
    expect(new Headers(init?.headers).get('X-CSRF')).toBe('1')
    expect(JSON.parse(String(init?.body))).toEqual({ username: 'backoffice@semogly.dev', password: 'Senha@123' })
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
