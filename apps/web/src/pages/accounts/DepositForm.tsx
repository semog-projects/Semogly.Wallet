import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, unwrap } from '@/api/client'
import { formatCurrency } from '@/lib/format'
import { MAX_DEPOSIT, parseAmount } from '@/lib/money'

/** Registro de depósito (entrada de dinheiro externo), exclusivo do papel operator. */
export function DepositForm({ accountId }: { accountId: string }) {
  const [raw, setRaw] = useState('')
  const queryClient = useQueryClient()
  const amount = parseAmount(raw)

  const deposit = useMutation({
    mutationFn: async (value: number) =>
      unwrap(
        await api.ledger.POST('/ledger/accounts/{accountId}/deposits', {
          params: { path: { accountId } },
          body: { amount: value },
        }),
      ),
    onSuccess: () => {
      setRaw('')
      // Extrato e livro mudam na hora (Ledger); o saldo do Accounts converge pelo evento
      void queryClient.invalidateQueries({ queryKey: ['ledger'] })
      void queryClient.invalidateQueries({ queryKey: ['accounts'] })
    },
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (amount !== null) {
      deposit.mutate(amount)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <div className="space-y-2">
        <Label htmlFor="deposit-amount">Valor do depósito</Label>
        <Input
          id="deposit-amount"
          inputMode="decimal"
          placeholder="0,00"
          value={raw}
          onChange={(event) => {
            setRaw(event.target.value)
            deposit.reset()
          }}
        />
        {raw && amount === null && (
          <p className="text-xs text-destructive">
            Informe um valor entre R$ 0,01 e {formatCurrency(MAX_DEPOSIT)}, com até duas casas decimais.
          </p>
        )}
      </div>

      <Button type="submit" disabled={amount === null || deposit.isPending}>
        {deposit.isPending ? 'Registrando…' : 'Registrar depósito'}
      </Button>

      {deposit.isSuccess && (
        <p role="status" className="text-sm text-emerald-700 dark:text-emerald-400">
          Depósito de {formatCurrency(deposit.data.amount)} registrado. Novo saldo: {formatCurrency(deposit.data.balanceAfter)}.
        </p>
      )}
      {deposit.isError && (
        <p role="alert" className="text-sm text-destructive">
          {deposit.error.message}
        </p>
      )}
    </form>
  )
}
