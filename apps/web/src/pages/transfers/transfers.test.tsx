import { act, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { classifyFailure } from '@/realtime/transfer-update'
import { mockFetch, renderApp } from '@/test/render'
import { hub } from '@/test/signalr-mock'

const operator = { path: '/bff/user', body: { username: 'backoffice', roles: ['operator'] } }
const source = '11111111-1111-1111-1111-111111111111'
const destination = '22222222-2222-2222-2222-222222222222'

const transfer = (id: string, status: string, failureReason: string | null) => ({
  id,
  sourceAccountId: source,
  destinationAccountId: destination,
  amount: 150,
  status,
  failureReason,
  createdAt: '2026-10-25T12:00:00Z',
  updatedAt: '2026-10-25T12:00:01Z',
})

const page = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })

describe('painel de transferências', () => {
  it('destaca a categoria de cada falha', async () => {
    mockFetch([
      operator,
      {
        path: '/api/transfers',
        body: page([
          transfer('t1', 'Completed', null),
          transfer('t2', 'Failed', 'Estornada: Conta não encontrada no ledger.'),
          transfer('t3', 'Failed', 'Limite diário de transferências excedido.'),
        ]),
      },
    ])

    renderApp('/transfers')

    const table = await screen.findByRole('table')
    expect(within(table).getByText('Estorno')).toBeInTheDocument()
    expect(within(table).getByText('Antifraude')).toBeInTheDocument()
    expect(within(table).getAllByText('Falhou')).toHaveLength(2)
  })

  it('conecta ao hub pela mesma origem e mostra eventos ao vivo, recarregando a lista', async () => {
    const fetchMock = mockFetch([operator, { path: '/api/transfers', body: page([]) }])

    renderApp('/transfers')
    expect(await screen.findByText('Tempo real conectado')).toBeInTheDocument()
    expect(hub.url).toBe('/hubs/transfers')

    const listCalls = () => fetchMock.mock.calls.filter(([input]) => String(input instanceof Request ? input.url : input).includes('/api/transfers')).length
    const before = listCalls()

    act(() =>
      hub.emit('TransferUpdated', {
        transferId: 't9',
        sourceAccountId: source,
        destinationAccountId: destination,
        amount: 42,
        status: 'Failed',
        reason: 'Saldo insuficiente.',
        occurredAt: '2026-10-25T12:00:00Z',
      }),
    )

    const feed = await screen.findByRole('list', { name: 'Atividade ao vivo' })
    expect(within(feed).getByText('R$ 42,00')).toBeInTheDocument()
    expect(within(feed).getByText('Recusada')).toBeInTheDocument()
    await vi.waitFor(() => expect(listCalls()).toBeGreaterThan(before))
  })

  it('filtro de status vai para a URL e para a consulta', async () => {
    const fetchMock = mockFetch([operator, { path: '/api/transfers', body: page([]) }])

    renderApp('/transfers')
    await userEvent.selectOptions(await screen.findByLabelText('Status'), 'Failed')

    await vi.waitFor(() => {
      const urls = fetchMock.mock.calls.map(([input]) => (input instanceof Request ? input.url : String(input)))
      expect(urls.some((url) => url.includes('/api/transfers') && url.includes('status=Failed'))).toBe(true)
    })
  })
})

describe('classifyFailure', () => {
  it.each([
    ['Estornada: Conta de destino bloqueada', 'refund'],
    ['Valor acima do limite por transação.', 'fraud'],
    ['Muitas transferências em sequência. Tente novamente em instantes.', 'fraud'],
    ['Saldo insuficiente.', 'rejected'],
    [null, null],
  ])('"%s" → %s', (reason, expected) => {
    expect(classifyFailure(reason)).toBe(expected)
  })
})
