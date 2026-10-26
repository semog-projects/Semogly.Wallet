import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Pagination } from '@/components/Pagination'
import { QueryState } from '@/components/QueryState'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { api, unwrap } from '@/api/client'
import { formatCurrency, formatDateTime } from '@/lib/format'
import { useDebouncedValue } from '@/lib/use-debounced-value'

const PAGE_SIZE = 20

/** Busca de contas por nome, e-mail ou CPF. Termo e página ficam na URL (voltar e links funcionam). */
export function AccountsPage() {
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? 1)
  const [term, setTerm] = useState(params.get('search') ?? '')
  const search = useDebouncedValue(term.trim())

  // Novo termo volta para a primeira página
  useEffect(() => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (search) next.set('search', search)
        else next.delete('search')
        if (search !== (current.get('search') ?? '')) next.delete('page')
        return next
      },
      { replace: true },
    )
  }, [search, setParams])

  const accounts = useQuery({
    queryKey: ['accounts', 'search', search, page],
    queryFn: async () =>
      unwrap(
        await api.accounts.GET('/accounts', {
          params: { query: { search: search || undefined, page, pageSize: PAGE_SIZE } },
        }),
      ),
    placeholderData: keepPreviousData,
  })

  function goToPage(next: number) {
    setParams((current) => {
      const updated = new URLSearchParams(current)
      updated.set('page', String(next))
      return updated
    })
  }

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-semibold">Contas</h1>

      <Input
        type="search"
        aria-label="Buscar contas"
        placeholder="Buscar por nome, e-mail ou CPF"
        value={term}
        onChange={(event) => setTerm(event.target.value)}
        className="max-w-md"
      />

      <QueryState query={accounts} loading="Buscando contas…">
        {(result) =>
          result.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nenhuma conta encontrada.</p>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Titular</TableHead>
                    <TableHead>E-mail</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Saldo</TableHead>
                    <TableHead>Aberta em</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((account) => (
                    <TableRow key={account.id}>
                      <TableCell>
                        <Link to={`/accounts/${account.id}`} className="font-medium underline-offset-4 hover:underline">
                          {account.ownerName}
                        </Link>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{account.email}</TableCell>
                      <TableCell>
                        <Badge variant={account.status === 'Active' ? 'secondary' : 'destructive'}>{account.status}</Badge>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatCurrency(account.balance)}</TableCell>
                      <TableCell className="text-muted-foreground">{formatDateTime(account.createdAt)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <p className="text-xs text-muted-foreground">{result.totalCount} conta(s)</p>
              <Pagination page={result.page} totalPages={result.totalPages ?? 1} onChange={goToPage} />
            </>
          )
        }
      </QueryState>
    </section>
  )
}
