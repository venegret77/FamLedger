import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, ApiError, apiUpload } from './client'
import type {
  AppSettings,
  BudgetContextInfo,
  BudgetSummary,
  CreateTransactionRequest,
  Debt,
  FamilyInfo,
  FamilyMemberRole,
  Income,
  OneOffExpense,
  RecurringExpense,
  SavingsResponse,
  Transaction,
  UserProfile,
} from './types'

export const queryKeys = {
  me: ['me'] as const,
  dashboard: ['dashboard'] as const,
  transactions: ['transactions'] as const,
  recurring: ['plan', 'recurring'] as const,
  oneOff: ['plan', 'one-off'] as const,
  incomes: ['plan', 'incomes'] as const,
  debts: ['debts'] as const,
  savings: ['savings', 'overview'] as const,
  family: ['family'] as const,
  settings: ['settings'] as const,
  categories: ['categories'] as const,
  contexts: ['contexts'] as const,
}

export function useMe() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: () => apiFetch<UserProfile>('/api/me'),
    retry: false,
    refetchOnWindowFocus: false,
  })
}

export function useDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: () => apiFetch<BudgetSummary>('/api/dashboard/summary'),
    staleTime: 0,
    retry: false,
  })
}

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories,
    queryFn: () => apiFetch<{ id: string; name: string; emoji?: string }[]>('/api/categories'),
  })
}

export function useTransactions() {
  return useQuery({
    queryKey: queryKeys.transactions,
    queryFn: () => apiFetch<Transaction[]>('/api/transactions'),
  })
}

export function useCreateTransaction() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload: CreateTransactionRequest) =>
      apiFetch<Transaction>('/api/transactions', {
        method: 'POST',
        body: payload,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard, refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: queryKeys.transactions, refetchType: 'all' })
    },
  })
}

export function useRecurringExpenses() {
  return useQuery({
    queryKey: queryKeys.recurring,
    queryFn: () => apiFetch<RecurringExpense[]>('/api/plan/recurring'),
  })
}

export function useOneOffExpenses() {
  return useQuery({
    queryKey: queryKeys.oneOff,
    queryFn: () => apiFetch<OneOffExpense[]>('/api/plan/one-off'),
  })
}

export function useIncomes() {
  return useQuery({
    queryKey: queryKeys.incomes,
    queryFn: () => apiFetch<Income[]>('/api/plan/incomes'),
  })
}

export function useDebts(hidePaid = false) {
  return useQuery({
    queryKey: [...queryKeys.debts, hidePaid] as const,
    queryFn: () => apiFetch<Debt[]>(`/api/debts?hidePaid=${hidePaid}`),
  })
}

export function useSavings() {
  return useQuery({
    queryKey: queryKeys.savings,
    queryFn: () => apiFetch<SavingsResponse>('/api/savings'),
  })
}

export function useFamily() {
  return useQuery({
    queryKey: queryKeys.family,
    queryFn: () => apiFetch<FamilyInfo>('/api/family'),
  })
}

export function useApproveJoinRequest() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ requestId, role }: { requestId: string; role: FamilyMemberRole }) =>
      apiFetch<void>(`/api/family/join-requests/${requestId}/approve`, {
        method: 'POST',
        body: { role },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.family, refetchType: 'all' })
    },
  })
}

export function useRejectJoinRequest() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (requestId: string) =>
      apiFetch<void>(`/api/family/join-requests/${requestId}/reject`, {
        method: 'POST',
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.family, refetchType: 'all' })
    },
  })
}

export function useRegenerateInviteCode() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (contextId: string) =>
      apiFetch<{ inviteCode: string }>(`/api/contexts/${contextId}/invite/regenerate`, {
        method: 'POST',
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.family, refetchType: 'all' })
    },
  })
}

export function useCreateFamily() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (name: string) =>
      apiFetch<{ id: string; name: string; inviteCode: string }>('/api/contexts/family', {
        method: 'POST',
        body: { name },
      }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.family, refetchType: 'all' }),
        queryClient.invalidateQueries({ queryKey: queryKeys.me, refetchType: 'all' }),
        queryClient.invalidateQueries({ queryKey: queryKeys.contexts, refetchType: 'all' }),
        queryClient.invalidateQueries({ queryKey: queryKeys.settings, refetchType: 'all' }),
      ])
    },
  })
}

export function useJoinFamily() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (inviteCode: string) =>
      apiFetch<{ id: string; status: string }>('/api/contexts/join', {
        method: 'POST',
        body: { inviteCode },
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.family })
      void queryClient.invalidateQueries({ queryKey: queryKeys.me })
    },
  })
}

export function useSettings() {
  return useQuery({
    queryKey: queryKeys.settings,
    queryFn: () => apiFetch<AppSettings>('/api/settings'),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      try {
        await apiFetch<void>('/api/auth/logout', { method: 'POST' })
      } catch (error) {
        if (!(error instanceof ApiError) || error.status !== 401) throw error
      }
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: queryKeys.me })
      queryClient.setQueryData(queryKeys.me, null)
    },
    onSettled: () => {
      queryClient.removeQueries({ queryKey: queryKeys.me })
    },
  })
}

function invalidatePlan(queryClient: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.recurring, refetchType: 'all' }),
    queryClient.invalidateQueries({ queryKey: queryKeys.oneOff, refetchType: 'all' }),
    queryClient.invalidateQueries({ queryKey: queryKeys.incomes, refetchType: 'all' }),
    queryClient.invalidateQueries({ queryKey: queryKeys.dashboard, refetchType: 'all' }),
  ])
}

function invalidateDebts(queryClient: ReturnType<typeof useQueryClient>) {
  return queryClient.invalidateQueries({ queryKey: queryKeys.debts })
}

export function useCreateCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (name: string) =>
      apiFetch<{ id: string; name: string }>('/api/categories', {
        method: 'POST',
        body: { name },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.categories })
      await queryClient.invalidateQueries({ queryKey: queryKeys.settings })
    },
  })
}

export function useUpdateCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) =>
      apiFetch<void>(`/api/categories/${id}`, { method: 'PATCH', body: { name } }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.categories })
      await queryClient.invalidateQueries({ queryKey: queryKeys.settings })
    },
  })
}

export function useDeleteCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/categories/${id}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.categories })
      await queryClient.invalidateQueries({ queryKey: queryKeys.settings })
    },
  })
}

export function useDeleteCategories() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (ids: string[]) => {
      await Promise.all(
        ids.map((id) => apiFetch<void>(`/api/categories/${id}`, { method: 'DELETE' })),
      )
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.categories })
      await queryClient.invalidateQueries({ queryKey: queryKeys.settings })
    },
  })
}

export function useCreateRecurring() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { name: string; amount: number; currency: string; chargeDay: number }) =>
      apiFetch<{ id: string }>('/api/plan/recurring', { method: 'POST', body: payload }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useDeleteRecurring() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (recurringExpenseId: string) =>
      apiFetch<void>(`/api/plan/recurring/expenses/${recurringExpenseId}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useToggleRecurringPaid() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (itemId: string) =>
      apiFetch<void>(`/api/plan/recurring/${itemId}/toggle-paid`, { method: 'PATCH' }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useToggleRecurringSkip() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (itemId: string) =>
      apiFetch<void>(`/api/plan/recurring/${itemId}/toggle-skip`, { method: 'PATCH' }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useCreateOneOff() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { name: string; amount: number; currency: string }) =>
      apiFetch<{ id: string }>('/api/plan/one-off', { method: 'POST', body: payload }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useDeleteOneOff() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/plan/one-off/${id}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useToggleOneOffPaid() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/plan/one-off/${id}/toggle-paid`, { method: 'PATCH' }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useCreateIncome() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { name: string; amount: number; currency: string }) =>
      apiFetch<{ id: string }>('/api/plan/incomes', { method: 'POST', body: payload }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useUpdateIncome() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      id,
      ...payload
    }: {
      id: string
      name: string
      amount: number
      currency: string
    }) => apiFetch<void>(`/api/plan/incomes/${id}`, { method: 'PATCH', body: payload }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useDeleteIncome() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/plan/incomes/${id}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useCreateGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { name: string; targetAmount: number; currency: string }) =>
      apiFetch<{ id: string }>('/api/savings/goals', { method: 'POST', body: payload }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.savings })
    },
  })
}

export function useDeleteGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (goalId: string) =>
      apiFetch<void>(`/api/savings/goals/${goalId}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.savings })
    },
  })
}

export function useUpdateRecurring() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      id,
      ...payload
    }: {
      id: string
      name: string
      amount: number
      currency: string
      chargeDay: number
    }) =>
      apiFetch<void>(`/api/plan/recurring/expenses/${id}`, { method: 'PATCH', body: payload }),
    onSuccess: async () => {
      await invalidatePlan(queryClient)
    },
  })
}

export function useCreateDebt() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: {
      counterpartyName: string
      counterpartyUserId?: string
      direction: 'OwedToUs' | 'WeOwe'
    }) =>
      apiFetch<{ id: string }>('/api/debts', {
        method: 'POST',
        body: {
          counterpartyName: payload.counterpartyName,
          counterpartyUserId: payload.counterpartyUserId ?? null,
          direction: payload.direction === 'OwedToUs' ? 1 : 0,
        },
      }),
    onSuccess: () => {
      void invalidateDebts(queryClient)
    },
  })
}

export function useAddDebtEntry() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      debtId,
      ...payload
    }: {
      debtId: string
      amount: number
      currency: string
      description?: string
    }) =>
      apiFetch<{ id: string }>(`/api/debts/${debtId}/entries`, { method: 'POST', body: payload }),
    onSuccess: () => {
      void invalidateDebts(queryClient)
    },
  })
}

export function useToggleDebtPaid() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (entryId: string) =>
      apiFetch<void>(`/api/debts/entries/${entryId}/toggle-paid`, { method: 'PATCH' }),
    onSuccess: () => {
      void invalidateDebts(queryClient)
    },
  })
}

export function useDeleteDebt() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (debtId: string) =>
      apiFetch<void>(`/api/debts/${debtId}`, { method: 'DELETE' }),
    onSuccess: () => {
      void invalidateDebts(queryClient)
    },
  })
}

export function useDeleteDebtEntry() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (entryId: string) =>
      apiFetch<void>(`/api/debts/entries/${entryId}`, { method: 'DELETE' }),
    onSuccess: () => {
      void invalidateDebts(queryClient)
    },
  })
}

export function useDepositSavings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ amount, currency }: { amount: number; currency: string }) =>
      apiFetch<void>('/api/savings/deposit', { method: 'POST', body: { amount, currency } }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.savings })
    },
  })
}

export function useSetSavingsPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ plannedAmount, currency }: { plannedAmount: number; currency: string }) =>
      apiFetch<void>('/api/savings/plan', {
        method: 'POST',
        body: { plannedAmount, currency },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.savings })
    },
  })
}

export function useContributeGoal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      goalId,
      amount,
      currency,
    }: {
      goalId: string
      amount: number
      currency: string
    }) =>
      apiFetch<void>(`/api/savings/goals/${goalId}/contribute`, {
        method: 'POST',
        body: { amount, currency },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.savings })
    },
  })
}

export function useUpdateBudgetSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { periodStartDay: number; baseCurrency: string }) =>
      apiFetch<void>('/api/settings', { method: 'PATCH', body: payload }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.settings })
      void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard, refetchType: 'all' })
      void queryClient.invalidateQueries({ queryKey: queryKeys.family })
    },
  })
}

export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (displayName: string) =>
      apiFetch<{ displayName: string }>('/api/users/me', {
        method: 'PATCH',
        body: { displayName },
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.me })
    },
  })
}

export function useUploadAvatar() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return apiUpload<{ avatarUrl: string }>('/api/users/me/avatar', form)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.me, refetchType: 'all' })
    },
  })
}

export function useDeleteTransaction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/transactions/${id}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.transactions, refetchType: 'all' })
      await queryClient.invalidateQueries({ queryKey: queryKeys.dashboard, refetchType: 'all' })
    },
  })
}

export function useContexts() {
  return useQuery({
    queryKey: queryKeys.contexts,
    queryFn: () => apiFetch<BudgetContextInfo[]>('/api/contexts'),
  })
}

export function useSwitchContext() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (contextId: string) =>
      apiFetch<void>('/api/contexts/switch', { method: 'POST', body: { contextId } }),
    onSuccess: () => {
      void queryClient.invalidateQueries()
    },
  })
}

export function useUpdateMemberRole() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      contextId,
      memberId,
      role,
    }: {
      contextId: string
      memberId: string
      role: FamilyMemberRole
    }) =>
      apiFetch<void>(`/api/contexts/${contextId}/members/role`, {
        method: 'PATCH',
        body: { memberId, role },
      }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.family, refetchType: 'all' }),
        queryClient.invalidateQueries({ queryKey: queryKeys.settings, refetchType: 'all' }),
      ])
    },
  })
}

export function useRemoveFamilyMember() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ contextId, memberId }: { contextId: string; memberId: string }) =>
      apiFetch<void>(`/api/contexts/${contextId}/members/${memberId}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.family, refetchType: 'all' }),
        queryClient.invalidateQueries({ queryKey: queryKeys.me, refetchType: 'all' }),
        queryClient.invalidateQueries({ queryKey: queryKeys.contexts, refetchType: 'all' }),
        queryClient.invalidateQueries({ queryKey: queryKeys.settings, refetchType: 'all' }),
      ])
    },
  })
}

export function usePermissions() {
  const { data: settings } = useSettings()
  return {
    canManagePlan: settings?.canManagePlan ?? true,
    canManageFamilySettings: settings?.canManageFamilySettings ?? true,
    myRole: settings?.myRole ?? 'Head',
  }
}
