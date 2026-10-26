import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { vi } from 'vitest'
import { AppRoutes } from '@/App'

/** Renderiza as rotas da aplicação numa URL inicial, com cache isolado por teste. */
export function renderApp(initialPath: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialPath]}>
        <AppRoutes />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

type Route = { method?: string; path: string; status?: number; body?: unknown }

/** Substitui o fetch global por respostas fixas por método + caminho; devolve o mock para inspeção. */
export function mockFetch(routes: Route[]) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const request = input instanceof Request ? input : new Request(new URL(String(input), 'http://localhost'), init)
    const path = new URL(request.url).pathname
    const route = routes.find((r) => r.path === path && (r.method ?? 'GET') === request.method)

    if (!route) {
      return new Response(null, { status: 404 })
    }

    return new Response(route.body === undefined ? null : JSON.stringify(route.body), {
      status: route.status ?? 200,
      headers: { 'Content-Type': 'application/json' },
    })
  })
}
