import { useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { api, unwrap } from '@/api/client'
import { formatCurrency } from '@/lib/format'

/** Visão do livro-razão: a soma de todas as contas precisa ser zero (partidas dobradas). */
export function OverviewPage() {
  const book = useQuery({
    queryKey: ['ledger', 'book'],
    queryFn: async () => unwrap(await api.ledger.GET('/ledger/book')),
  })

  if (book.isPending) {
    return <p className="text-sm text-muted-foreground">Carregando o livro…</p>
  }

  if (book.isError) {
    return (
      <p role="alert" className="text-sm text-destructive">
        {book.error.message}
      </p>
    )
  }

  const { cashIn, clearing, customerBalances, customerAccounts, total } = book.data

  return (
    <section className="space-y-6">
      <div className="flex items-center gap-3">
        <h1 className="text-2xl font-semibold">Visão geral</h1>
        {total === 0 ? <Badge>Livro balanceado</Badge> : <Badge variant="destructive">Livro desbalanceado</Badge>}
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Metric title="Saldo dos clientes" value={customerBalances} hint={`${customerAccounts} contas com movimento`} />
        <Metric title="CashIn" value={cashIn} hint="Contrapartida dos depósitos" />
        <Metric title="Clearing" value={clearing} hint="Em trânsito nas transferências" />
        <Metric title="Total do livro" value={total} hint="Deve ser sempre zero" />
      </div>
    </section>
  )
}

function Metric({ title, value, hint }: { title: string; value: number; hint: string }) {
  return (
    <Card>
      <CardHeader>
        <CardDescription>{title}</CardDescription>
        <CardTitle className="text-2xl tabular-nums">{formatCurrency(value)}</CardTitle>
      </CardHeader>
      <CardContent className="text-xs text-muted-foreground">{hint}</CardContent>
    </Card>
  )
}
