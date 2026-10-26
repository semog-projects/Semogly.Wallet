import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { Pagination } from '@/components/Pagination'
import { QueryState } from '@/components/QueryState'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { api, unwrap } from '@/api/client'
import type { components } from '@/api/schema/transfers'
import { formatCurrency, formatDateTime } from '@/lib/format'
import { LiveFeed } from './LiveFeed'
import { TransferStatusBadge } from './TransferStatusBadge'

type TransferStatus = components['schemas']['TransferStatus']

const PAGE_SIZE = 20
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const statusOptions: { value: TransferStatus | ''; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'Pending', label: 'Pendentes' },
  { value: 'Completed', label: 'Concluídas' },
  { value: 'Failed', label: 'Com falha' },
]

/** Data local (yyyy-mm-dd) → início ou fim do dia, em ISO. */
function toIso(date: string, endOfDay: boolean): string | undefined {
  return date ? new Date(`${date}T${endOfDay ? '23:59:59.999' : '00:00:00'}`).toISOString() : undefined
}

/**
 * Painel de transferências do backoffice. Filtros na URL; a lista é revalidada a cada evento
 * do SignalR (o RealtimeProvider invalida as queries de transferências).
 */
export function TransfersPage() {
  const [params, setParams] = useSearchParams()
  const status = (params.get('status') ?? '') as TransferStatus | ''
  const account = params.get('account') ?? ''
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''
  const page = Number(params.get('page') ?? 1)
  const accountId = GUID.test(account.trim()) ? account.trim() : undefined

  const transfers = useQuery({
    queryKey: ['transfers', 'list', status, accountId, from, to, page],
    queryFn: async () =>
      unwrap(
        await api.transfers.GET('/transfers', {
          params: {
            query: {
              status: status || undefined,
              accountId,
              from: toIso(from, false),
              to: toIso(to, true),
              page,
              pageSize: PAGE_SIZE,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  })

  function setFilter(name: string, value: string) {
    setParams((current) => {
      const next = new URLSearchParams(current)
      if (value) next.set(name, value)
      else next.delete(name)
      if (name !== 'page') next.delete('page')
      return next
    })
  }

  return (
    <section className="grid gap-6 xl:grid-cols-[1fr_20rem]">
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold">Transferências</h1>

        <div className="flex flex-wrap items-end gap-3">
          <div className="space-y-1">
            <Label htmlFor="status">Status</Label>
            <select
              id="status"
              value={status}
              onChange={(event) => setFilter('status', event.target.value)}
              className="h-9 rounded-md border bg-background px-2 text-sm"
            >
              {statusOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="account">Conta (origem ou destino)</Label>
            <Input
              id="account"
              placeholder="Id da conta"
              value={account}
              onChange={(event) => setFilter('account', event.target.value)}
              className="w-80"
              aria-invalid={account !== '' && !accountId}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="from">De</Label>
            <Input id="from" type="date" value={from} onChange={(event) => setFilter('from', event.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="to">Até</Label>
            <Input id="to" type="date" value={to} onChange={(event) => setFilter('to', event.target.value)} />
          </div>
        </div>

        <QueryState query={transfers} loading="Carregando transferências…">
          {(result) =>
            result.items.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nenhuma transferência encontrada.</p>
            ) : (
              <>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Data</TableHead>
                      <TableHead>Origem → destino</TableHead>
                      <TableHead className="text-right">Valor</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Motivo</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {result.items.map((transfer) => (
                      <TableRow key={transfer.id} className={transfer.status === 'Failed' ? 'bg-destructive/5' : undefined}>
                        <TableCell className="text-muted-foreground">{formatDateTime(transfer.createdAt)}</TableCell>
                        <TableCell className="font-mono text-xs">
                          <AccountLink id={transfer.sourceAccountId} /> → <AccountLink id={transfer.destinationAccountId} />
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{formatCurrency(transfer.amount)}</TableCell>
                        <TableCell>
                          <TransferStatusBadge status={transfer.status} reason={transfer.failureReason} />
                        </TableCell>
                        <TableCell className="max-w-72 text-sm text-muted-foreground">{transfer.failureReason}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                <p className="text-xs text-muted-foreground">{result.totalCount} transferência(s)</p>
                <Pagination
                  page={result.page}
                  totalPages={result.totalPages ?? 1}
                  onChange={(next) => setFilter('page', String(next))}
                />
              </>
            )
          }
        </QueryState>
      </div>

      <LiveFeed />
    </section>
  )
}

function AccountLink({ id }: { id: string }) {
  return (
    <Link to={`/accounts/${id}`} title={id} className="underline-offset-4 hover:underline">
      {id.slice(0, 8)}
    </Link>
  )
}
