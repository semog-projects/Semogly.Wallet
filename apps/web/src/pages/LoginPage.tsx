import { Navigate, useLocation } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { loginUrl } from '@/api/bff'
import { isOperator, useSession } from '@/auth/session'
import { redirectTo } from '@/lib/navigation'

/** Login pelo Keycloak (Authorization Code + PKCE feito pelo Gateway); o web nunca vê usuário, senha ou token. */
export function LoginPage() {
  const { data: user } = useSession()
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/'

  if (isOperator(user)) {
    return <Navigate to={from} replace />
  }

  return (
    <main className="flex min-h-svh items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Semogly Wallet · Backoffice</CardTitle>
          <CardDescription>
            Acesso restrito a operadores. Em desenvolvimento: backoffice@semogly.dev / Senha@123
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button className="w-full" onClick={() => redirectTo(loginUrl(from))}>
            Entrar com Keycloak
          </Button>
        </CardContent>
      </Card>
    </main>
  )
}
