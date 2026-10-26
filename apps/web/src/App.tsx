import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router'
import { ApiError } from '@/api/client'
import { RequireOperator } from '@/auth/RequireOperator'
import { AppLayout } from '@/layout/AppLayout'
import { AccountDetailPage } from '@/pages/accounts/AccountDetailPage'
import { AccountsPage } from '@/pages/accounts/AccountsPage'
import { LoginPage } from '@/pages/LoginPage'
import { OverviewPage } from '@/pages/OverviewPage'
import { TransfersPage } from '@/pages/transfers/TransfersPage'
import { RealtimeProvider } from '@/realtime/RealtimeProvider'

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
            <RealtimeProvider>
              <AppLayout />
            </RealtimeProvider>
          </RequireOperator>
        }
      >
        <Route index element={<OverviewPage />} />
        <Route path="accounts" element={<AccountsPage />} />
        <Route path="accounts/:id" element={<AccountDetailPage />} />
        <Route path="transfers" element={<TransfersPage />} />
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
