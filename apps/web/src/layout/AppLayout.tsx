import { NavLink, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import { useLogout, useSession } from '@/auth/session'
import { cn } from '@/lib/utils'
import { useRealtimeStatus } from '@/realtime/realtime-context'

const navigation = [
  { to: '/', label: 'Visão geral', end: true },
  { to: '/accounts', label: 'Contas' },
  { to: '/transfers', label: 'Transferências' },
]

const realtimeLabels = {
  connected: 'Tempo real conectado',
  connecting: 'Conectando ao tempo real…',
  disconnected: 'Tempo real desconectado',
} as const

export function AppLayout() {
  const { data: user } = useSession()
  const logout = useLogout()
  const realtime = useRealtimeStatus()

  return (
    <div className="flex min-h-svh">
      <aside className="hidden w-56 shrink-0 border-r bg-muted/30 p-4 md:block">
        <p className="mb-6 px-2 text-sm font-semibold">Semogly · Backoffice</p>
        <nav className="space-y-1">
          {navigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                cn(
                  'block rounded-md px-2 py-1.5 text-sm hover:bg-muted',
                  isActive && 'bg-muted font-medium text-foreground',
                )
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex items-center justify-end gap-3 border-b px-6 py-3">
          <span className="mr-auto flex items-center gap-2 text-xs text-muted-foreground" aria-live="polite">
            <span
              className={cn(
                'size-2 rounded-full',
                realtime === 'connected' ? 'bg-emerald-500' : realtime === 'connecting' ? 'bg-amber-500' : 'bg-red-500',
              )}
            />
            {realtimeLabels[realtime]}
          </span>
          <span className="text-sm text-muted-foreground">{user?.username}</span>
          <Button variant="outline" size="sm" onClick={() => logout.mutate()}>
            Sair
          </Button>
        </header>
        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
