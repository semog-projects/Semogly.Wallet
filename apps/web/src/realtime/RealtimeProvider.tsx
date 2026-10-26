import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { RealtimeContext, type ConnectionStatus, type TransferListener } from './realtime-context'
import type { TransferUpdate } from './transfer-update'

const HUB_URL = '/hubs/transfers'

/**
 * Uma conexão SignalR por sessão de operador. No navegador ela passa pelo proxy do Vite (mesma origem)
 * e é autenticada pelo cookie do BFF, sem token no JavaScript. Operadores recebem todas as transferências.
 */
export function RealtimeProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const listeners = useRef(new Set<TransferListener>())
  const [status, setStatus] = useState<ConnectionStatus>('connecting')

  useEffect(() => {
    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('TransferUpdated', (update: TransferUpdate) => {
      listeners.current.forEach((listener) => listener(update))

      // Mantém listas e o livro coerentes com o que acabou de acontecer
      void queryClient.invalidateQueries({ queryKey: ['transfers'] })
      if (update.status !== 'Pending') {
        void queryClient.invalidateQueries({ queryKey: ['ledger'] })
        void queryClient.invalidateQueries({ queryKey: ['accounts'] })
      }
    })

    connection.onreconnecting(() => setStatus('connecting'))
    connection.onreconnected(() => setStatus('connected'))
    connection.onclose(() => setStatus('disconnected'))

    let cancelled = false
    connection
      .start()
      .then(() => !cancelled && setStatus('connected'))
      .catch(() => !cancelled && setStatus('disconnected'))

    return () => {
      cancelled = true
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop()
      }
    }
  }, [queryClient])

  // Estável entre renders: quem assina não reassina a cada mudança de status
  const subscribe = useCallback((listener: TransferListener) => {
    listeners.current.add(listener)
    return () => {
      listeners.current.delete(listener)
    }
  }, [])

  const value = useMemo(() => ({ status, subscribe }), [status, subscribe])

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>
}
