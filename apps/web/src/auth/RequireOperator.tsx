import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { Button } from '@/components/ui/button'
import { isOperator, useLogout, useSession } from './session'

/** Protege as rotas do backoffice: exige sessão e o papel operator. */
export function RequireOperator({ children }: { children: ReactNode }) {
  const { data: user, isPending } = useSession()
  const logout = useLogout()
  const location = useLocation()

  if (isPending) {
    return <p className="p-8 text-sm text-muted-foreground">Verificando sessão…</p>
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (!isOperator(user)) {
    return (
      <div className="mx-auto mt-24 max-w-md space-y-4 p-8 text-center">
        <h1 className="text-xl font-semibold">Acesso restrito</h1>
        <p className="text-sm text-muted-foreground">
          O backoffice é exclusivo para operadores. Você entrou como <strong>{user.username}</strong>, sem esse papel.
        </p>
        <Button variant="outline" onClick={() => logout.mutate()}>
          Sair
        </Button>
      </div>
    )
  }

  return children
}
