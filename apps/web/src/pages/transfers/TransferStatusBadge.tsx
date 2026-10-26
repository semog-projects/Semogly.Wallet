import { Badge } from '@/components/ui/badge'
import { classifyFailure, failureLabels } from '@/realtime/transfer-update'

const statusLabels = { Pending: 'Pendente', Completed: 'Concluída', Failed: 'Falhou' } as const

type Props = {
  status: keyof typeof statusLabels
  reason?: string | null
}

/** Status da transferência; nas falhas, também a categoria (antifraude, estorno ou recusa). */
export function TransferStatusBadge({ status, reason }: Props) {
  const failure = status === 'Failed' ? classifyFailure(reason) : null

  return (
    <span className="inline-flex items-center gap-1">
      <Badge variant={status === 'Failed' ? 'destructive' : status === 'Completed' ? 'secondary' : 'outline'}>
        {statusLabels[status]}
      </Badge>
      {failure && <Badge variant="outline">{failureLabels[failure]}</Badge>}
    </span>
  )
}
