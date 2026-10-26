import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { isOperator, useLogin, useSession } from '@/auth/session'

/**
 * Login de desenvolvimento: o Gateway emite a sessão para qualquer usuário.
 * Em produção, este botão iniciaria o fluxo OIDC com o Identity Provider.
 */
export function LoginPage() {
  const [username, setUsername] = useState('backoffice')
  const [asOperator, setAsOperator] = useState(true)
  const { data: user } = useSession()
  const login = useLogin()
  const navigate = useNavigate()
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/'

  if (isOperator(user)) {
    return <Navigate to={from} replace />
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    login.mutate({ username: username.trim(), asOperator }, { onSuccess: () => navigate(from, { replace: true }) })
  }

  return (
    <main className="flex min-h-svh items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Semogly Wallet · Backoffice</CardTitle>
          <CardDescription>Ambiente de desenvolvimento: a sessão é criada pelo Gateway (BFF).</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="username">Usuário</Label>
              <Input
                id="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                autoComplete="username"
                required
              />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="operator"
                checked={asOperator}
                onCheckedChange={(checked) => setAsOperator(checked === true)}
              />
              <Label htmlFor="operator">Entrar como operador</Label>
            </div>
            {login.isError && (
              <p role="alert" className="text-sm text-destructive">
                {login.error.message}
              </p>
            )}
            <Button type="submit" className="w-full" disabled={login.isPending || !username.trim()}>
              {login.isPending ? 'Entrando…' : 'Entrar'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  )
}
