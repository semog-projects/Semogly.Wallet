/** Evento TransferUpdated enviado pelo hub /hubs/transfers do Gateway. */
export type TransferUpdate = {
  transferId: string
  sourceAccountId: string
  destinationAccountId: string
  amount: number
  status: 'Pending' | 'Completed' | 'Failed'
  reason: string | null
  occurredAt: string
}

export type FailureKind = 'fraud' | 'refund' | 'rejected'

// Motivos publicados pelo Fraud (FraudRejectionExtensions.ToReason) e prefixo do estorno da saga
const FRAUD_REASONS = [
  'Valor acima do limite por transação.',
  'Limite diário de transferências excedido.',
  'Muitas transferências em sequência. Tente novamente em instantes.',
]
const REFUND_PREFIX = 'Estornada: '

/** Classifica a falha para o operador: antifraude, estorno (compensação da saga) ou recusa do Ledger. */
export function classifyFailure(reason: string | null | undefined): FailureKind | null {
  if (!reason) {
    return null
  }

  if (reason.startsWith(REFUND_PREFIX)) {
    return 'refund'
  }

  return FRAUD_REASONS.includes(reason) ? 'fraud' : 'rejected'
}

export const failureLabels: Record<FailureKind, string> = {
  fraud: 'Antifraude',
  refund: 'Estorno',
  rejected: 'Recusada',
}
