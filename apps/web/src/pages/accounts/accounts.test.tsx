import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockFetch, renderApp } from '@/test/render'
import { parseAmount } from '@/lib/money'

const operator = { path: '/bff/user', body: { username: 'backoffice', roles: ['operator'] } }
const accountId = '01a17549-eebd-77e7-938b-c46e43aceeaa'
const account = {
  id: accountId,
  ownerName: 'Ana Souza',
  email: 'ana@email.com',
  currency: 'BRL',
  balance: 300,
  status: 'Active',
  createdAt: '2026-10-25T12:00:00Z',
}

describe('contas', () => {
  it('lista o resultado da busca com link para o detalhe', async () => {
    mockFetch([
      operator,
      { path: '/api/accounts', body: { items: [account], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 } },
    ])

    renderApp('/accounts')

    const link = await screen.findByRole('link', { name: 'Ana Souza' })
    expect(link).toHaveAttribute('href', `/accounts/${accountId}`)
    expect(screen.getByText('R$ 300,00')).toBeInTheDocument()
  })

  it('registra depósito e mostra o novo saldo informado pelo Ledger', async () => {
    const fetchMock = mockFetch([
      operator,
      { path: `/api/accounts/${accountId}`, body: account },
      {
        path: `/api/ledger/accounts/${accountId}/statement`,
        body: { accountId, balance: 300, page: 1, pageSize: 10, entries: [] },
      },
      {
        path: `/api/fraud/accounts/${accountId}/daily-usage`,
        body: { accountId, date: '2026-10-25', used: 2500, limit: 10000, remaining: 7500 },
      },
      { path: `/api/notifications/accounts/${accountId}`, body: [] },
      {
        method: 'POST',
        path: `/api/ledger/accounts/${accountId}/deposits`,
        body: { entryId: 'e1', accountId, amount: 1234.56, balanceAfter: 1534.56, occurredAt: '2026-10-25T12:00:00Z' },
      },
    ])

    renderApp(`/accounts/${accountId}`)

    expect(await screen.findByText('R$ 2.500,00 de R$ 10.000,00')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Valor do depósito'), '1.234,56')
    await userEvent.click(screen.getByRole('button', { name: 'Registrar depósito' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Novo saldo: R$ 1.534,56')

    const depositRequest = fetchMock.mock.calls
      .map(([input]) => input)
      .find((input): input is Request => input instanceof Request && input.method === 'POST')!
    await waitFor(async () => expect(await depositRequest.clone().json()).toEqual({ amount: 1234.56 }))
    expect(depositRequest.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
  })
})

describe('parseAmount', () => {
  it.each([
    ['1.234,56', 1234.56],
    ['1234,5', 1234.5],
    ['1234.56', 1234.56],
    ['100', 100],
  ])('interpreta "%s" como %d', (raw, expected) => {
    expect(parseAmount(raw)).toBe(expected)
  })

  it.each(['0', '-10', '1,234', 'abc', '2000000'])('recusa "%s"', (raw) => {
    expect(parseAmount(raw)).toBeNull()
  })
})
