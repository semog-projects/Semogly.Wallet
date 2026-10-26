import { createContext, useContext, useEffect, useState } from 'react'
import type { TransferUpdate } from './transfer-update'

export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected'
export type TransferListener = (update: TransferUpdate) => void

type RealtimeContextValue = {
  status: ConnectionStatus
  subscribe: (listener: TransferListener) => () => void
}

export const RealtimeContext = createContext<RealtimeContextValue>({
  status: 'disconnected',
  subscribe: () => () => {},
})

export function useRealtimeStatus(): ConnectionStatus {
  return useContext(RealtimeContext).status
}

/** Últimas atualizações recebidas, da mais recente para a mais antiga. */
export function useTransferFeed(limit = 8): TransferUpdate[] {
  const { subscribe } = useContext(RealtimeContext)
  const [feed, setFeed] = useState<TransferUpdate[]>([])

  useEffect(() => subscribe((update) => setFeed((current) => [update, ...current].slice(0, limit))), [subscribe, limit])

  return feed
}
