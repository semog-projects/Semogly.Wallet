import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { isOperator, useLogin, useSession } from '@/auth/session'

/**
 * Login de desenvolvimento: o Gateway (BFF) obtém o token do Keycloak com usuário e senha e cria a sessão.
 * O fluxo OIDC com redirecionamento para a tela do Keycloak chega na issue #40.
 */
export function LoginPage() {
  const [username, setUsername] = useState('backoffice@semogly.dev')
  const [password, setPassword] = useState('')
  const { data: user } = useSession()
  const login = useLogin()
  const navigate = useNavigate()
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/'

  if (isOperator(user)) {
    return <Navigate to={from} replace />
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    login.mutate({ username: username.trim(), password }, { onSuccess: () => navigate(from, { replace: true }) })
  }

  return (
    <main className="flex min-h-svh items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Semogly Wallet · Backoffice</CardTitle>
          <CardDescription>
            Usuários do Keycloak (ambiente de desenvolvimento). Operador: backoffice@semogly.dev / Senha@123
          </CardDescription>
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
            <div className="space-y-2">
              <Label htmlFor="password">Senha</Label>
              <Input
                id="password"
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
                required
              />
            </div>
            {login.isError && (
              <p role="alert" className="text-sm text-destructive">
                {login.error.message}
              </p>
            )}
            <Button type="submit" className="w-full" disabled={login.isPending || !username.trim() || !password}>
              {login.isPending ? 'Entrando…' : 'Entrar'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  )
}
