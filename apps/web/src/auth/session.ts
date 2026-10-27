import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getSession, logout, OPERATOR_ROLE, type SessionUser } from '@/api/bff'

const sessionKey = ['session'] as const

export function useSession() {
  return useQuery({
    queryKey: sessionKey,
    queryFn: getSession,
    staleTime: 60_000,
    retry: false,
  })
}

export function isOperator(user: SessionUser | null | undefined): boolean {
  return user?.roles.includes(OPERATOR_ROLE) ?? false
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: logout,
    // Descarta tudo que foi carregado na sessão anterior
    onSuccess: () => {
      queryClient.clear()
      queryClient.setQueryData(sessionKey, null)
    },
  })
}
