import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router'
import { ApiError } from '@/api/client'
import { RequireOperator } from '@/auth/RequireOperator'
import { AppLayout } from '@/layout/AppLayout'
import { ComingSoonPage } from '@/pages/ComingSoonPage'
import { LoginPage } from '@/pages/LoginPage'
import { OverviewPage } from '@/pages/OverviewPage'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Erros de autorização não melhoram com nova tentativa
      retry: (failureCount, error) =>
        !(error instanceof ApiError && [401, 403].includes(error.status)) && failureCount < 2,
    },
  },
})

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireOperator>
            <AppLayout />
          </RequireOperator>
        }
      >
        <Route index element={<OverviewPage />} />
        <Route path="accounts" element={<ComingSoonPage title="Contas" issue={25} />} />
        <Route path="transfers" element={<ComingSoonPage title="Transferências" issue={26} />} />
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </QueryClientProvider>
  )
}
