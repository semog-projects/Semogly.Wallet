import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCurrency, formatDateTime } from '@/lib/format'
import { useTransferFeed } from '@/realtime/realtime-context'
import { TransferStatusBadge } from './TransferStatusBadge'

/** Eventos de transferência chegando pelo SignalR, do mais recente para o mais antigo. */
export function LiveFeed() {
  const feed = useTransferFeed()

  return (
    <Card>
      <CardHeader>
        <CardTitle>Atividade ao vivo</CardTitle>
        <CardDescription>Eventos da saga recebidos em tempo real</CardDescription>
      </CardHeader>
      <CardContent>
        {feed.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aguardando eventos…</p>
        ) : (
          <ul aria-label="Atividade ao vivo" className="divide-y">
            {feed.map((update) => (
              <li key={`${update.transferId}-${update.status}`} className="flex flex-wrap items-center gap-2 py-2 text-sm">
                <TransferStatusBadge status={update.status} reason={update.reason} />
                <span className="tabular-nums">{formatCurrency(update.amount)}</span>
                <span className="text-xs text-muted-foreground">{formatDateTime(update.occurredAt)}</span>
                {update.reason && <span className="w-full text-xs text-muted-foreground">{update.reason}</span>}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
