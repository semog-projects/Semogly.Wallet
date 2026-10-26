import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { QueryState } from '@/components/QueryState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { api, unwrap } from '@/api/client'
import { formatCurrency, formatDateTime } from '@/lib/format'
import { DepositForm } from './DepositForm'

const STATEMENT_PAGE_SIZE = 10

export function AccountDetailPage() {
  const { id = '' } = useParams()

  const account = useQuery({
    queryKey: ['accounts', id],
    queryFn: async () => unwrap(await api.accounts.GET('/accounts/{id}', { params: { path: { id } } })),
  })

  return (
    <section className="space-y-6">
      <Link to="/accounts" className="text-sm text-muted-foreground hover:underline">
        ← Contas
      </Link>

      <QueryState query={account} loading="Carregando conta…">
        {(data) => (
          <>
            <header className="flex flex-wrap items-center gap-3">
              <h1 className="text-2xl font-semibold">{data.ownerName}</h1>
              <Badge variant={data.status === 'Active' ? 'secondary' : 'destructive'}>{data.status}</Badge>
              <span className="text-sm text-muted-foreground">
                {data.email} · aberta em {formatDateTime(data.createdAt)}
              </span>
            </header>

            <div className="grid gap-4 lg:grid-cols-3">
              <BalanceCard accountId={id} />
              <DailyLimitCard accountId={id} />
              <Card>
                <CardHeader>
                  <CardTitle>Depósito</CardTitle>
                  <CardDescription>Simula a entrada de um PIX ou boleto</CardDescription>
                </CardHeader>
                <CardContent>
                  <DepositForm accountId={id} />
                </CardContent>
              </Card>
            </div>

            <Statement accountId={id} />
            <Notifications accountId={id} />
          </>
        )}
      </QueryState>
    </section>
  )
}

function useStatement(accountId: string, page: number) {
  return useQuery({
    queryKey: ['ledger', 'statement', accountId, page],
    queryFn: async () =>
      unwrap(
        await api.ledger.GET('/ledger/accounts/{accountId}/statement', {
          params: { path: { accountId }, query: { page, pageSize: STATEMENT_PAGE_SIZE } },
        }),
      ),
    placeholderData: keepPreviousData,
  })
}

/** Saldo do Ledger (fonte da verdade): reflete o depósito na hora, sem esperar a projeção do Accounts. */
function BalanceCard({ accountId }: { accountId: string }) {
  const statement = useStatement(accountId, 1)

  return (
    <Card>
      <CardHeader>
        <CardDescription>Saldo (Ledger)</CardDescription>
        <CardTitle className="text-3xl tabular-nums">
          {statement.data ? formatCurrency(statement.data.balance) : '—'}
        </CardTitle>
      </CardHeader>
      <CardContent className="text-xs text-muted-foreground">Calculado a partir dos lançamentos do livro-razão</CardContent>
    </Card>
  )
}

function DailyLimitCard({ accountId }: { accountId: string }) {
  const usage = useQuery({
    queryKey: ['fraud', 'daily-usage', accountId],
    queryFn: async () =>
      unwrap(await api.fraud.GET('/fraud/accounts/{accountId}/daily-usage', { params: { path: { accountId } } })),
  })

  return (
    <Card>
      <CardHeader>
        <CardDescription>Limite diário de transferências</CardDescription>
        <CardTitle className="text-xl tabular-nums">
          {usage.data ? `${formatCurrency(usage.data.used)} de ${formatCurrency(usage.data.limit)}` : '—'}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        <Progress value={usage.data ? (usage.data.used / usage.data.limit) * 100 : 0} aria-label="Uso do limite diário" />
        {usage.data && (
          <p className="text-xs text-muted-foreground">Disponível hoje: {formatCurrency(usage.data.remaining)}</p>
        )}
      </CardContent>
    </Card>
  )
}

function Statement({ accountId }: { accountId: string }) {
  const [page, setPage] = useState(1)
  const statement = useStatement(accountId, page)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Extrato</CardTitle>
        <CardDescription>Lançamentos do mais recente para o mais antigo</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <QueryState query={statement} loading="Carregando extrato…">
          {(data) =>
            data.entries.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nenhum lançamento.</p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Data</TableHead>
                    <TableHead>Descrição</TableHead>
                    <TableHead className="text-right">Valor</TableHead>
                    <TableHead className="text-right">Saldo após</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.entries.map((entry) => (
                    <TableRow key={entry.entryId}>
                      <TableCell className="text-muted-foreground">{formatDateTime(entry.occurredAt)}</TableCell>
                      <TableCell>{entry.description}</TableCell>
                      <TableCell
                        className={`text-right tabular-nums ${entry.type === 'Debit' ? 'text-destructive' : 'text-emerald-700 dark:text-emerald-400'}`}
                      >
                        {entry.type === 'Debit' ? '−' : '+'}
                        {formatCurrency(entry.amount)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatCurrency(entry.balanceAfter)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )
          }
        </QueryState>

        {/* O extrato não informa o total: há próxima página se esta veio cheia */}
        <div className="flex justify-end gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
            Anterior
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={(statement.data?.entries.length ?? 0) < STATEMENT_PAGE_SIZE}
            onClick={() => setPage(page + 1)}
          >
            Próxima
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

function Notifications({ accountId }: { accountId: string }) {
  const notifications = useQuery({
    queryKey: ['notifications', accountId],
    queryFn: async () =>
      unwrap(
        await api.notifications.GET('/notifications/accounts/{accountId}', {
          params: { path: { accountId }, query: { limit: 10 } },
        }),
      ),
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Notificações enviadas</CardTitle>
        <CardDescription>Últimas 10 mensagens ao titular</CardDescription>
      </CardHeader>
      <CardContent>
        <QueryState query={notifications} loading="Carregando notificações…">
          {(items) =>
            items.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nenhuma notificação.</p>
            ) : (
              <ul className="divide-y">
                {items.map((notification) => (
                  <li key={notification.id} className="py-2">
                    <p className="text-sm font-medium">{notification.subject}</p>
                    <p className="text-sm text-muted-foreground">{notification.body}</p>
                    <p className="text-xs text-muted-foreground">{formatDateTime(notification.createdAt)}</p>
                  </li>
                ))}
              </ul>
            )
          }
        </QueryState>
      </CardContent>
    </Card>
  )
}
