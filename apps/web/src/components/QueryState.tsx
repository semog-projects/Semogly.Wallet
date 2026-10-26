import type { ReactNode } from 'react'

type Props<T> = {
  query: { isPending: boolean; isError: boolean; error: Error | null; data: T | undefined }
  loading?: string
  children: (data: T) => ReactNode
}

/** Estados padrão de uma query: carregando, erro ou conteúdo. */
export function QueryState<T>({ query, loading = 'Carregando…', children }: Props<T>) {
  if (query.isPending) {
    return <p className="text-sm text-muted-foreground">{loading}</p>
  }

  if (query.isError) {
    return (
      <p role="alert" className="text-sm text-destructive">
        {query.error?.message}
      </p>
    )
  }

  return children(query.data as T)
}
