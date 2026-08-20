export const CURRENCIES = ['RSD', 'EUR', 'USD'] as const
export type CurrencyCode = (typeof CURRENCIES)[number]

export const currencyOptions = CURRENCIES.map((c) => ({ value: c, label: c }))
export type FamilyMemberRole = 'Head' | 'Assistant' | 'Member'
export type JoinRequestStatus = 'Pending' | 'Approved' | 'Rejected'
export type DebtDirection = 'OwedToUs' | 'WeOwe'

export interface BudgetSummary {
  income: number
  plannedExpenses: number
  spent: number
  carryover: number
  remaining: number
  dailyBudgetAtStart: number
  dailyBudgetNow: number
  availableToday: number
  spentToday: number
  daysInPeriod: number
  daysPassed: number
  daysRemaining: number
  periodLabel: string
  periodId: string
  currency: string
}

export interface Category {
  id: string
  name: string
  emoji?: string
}

export interface Transaction {
  id: string
  amount: number
  baseAmount: number
  currency: string
  date: string
  note?: string
  categoryId?: string
  categoryName?: string
  createdByName?: string
  createdAt: string
}

export interface CreateTransactionRequest {
  amount: number
  currency?: string
  categoryId?: string
  note?: string
  date?: string
}

export interface RecurringExpense {
  id: string
  recurringExpenseId?: string
  name: string
  definitionAmount: number
  definitionCurrency: string
  chargeDayOfMonth: number
  categoryName?: string
  periodAmount?: number
  plannedBaseAmount?: number
  isPaid?: boolean
  isSkipped?: boolean
}

export interface OneOffExpense {
  id: string
  name: string
  amount: number
  currency: string
  baseAmount: number
  isPaid: boolean
}

export interface Income {
  id: string
  name: string
  amount: number
  currency: string
  sortOrder: number
  baseAmount?: number
}

export interface DebtEntry {
  id: string
  amount: number
  currency: string
  description?: string
  isPaid: boolean
  createdAt: string
}

export interface Debt {
  id: string
  counterpartyName: string
  counterpartyUserId?: string
  direction: DebtDirection
  balance: number
  currency: string
  entries: DebtEntry[]
}

export interface SavingsResponse {
  balance: number
  current: { plannedAmount: number; actualAmount: number }
  plans: SavingsEntry[]
  goals: SavingsGoal[]
}

export interface SavingsGoal {
  id: string
  name: string
  targetAmount: number
  isCompleted: boolean
  progress: number
}

export interface SavingsEntry {
  id: string
  plannedAmount: number
  actualAmount: number
  currency: string
  periodLabel?: string
  periodStart?: string
  periodEnd?: string
}

export interface FamilyMember {
  id: string
  userId: string
  displayName: string
  username?: string
  role: FamilyMemberRole
  joinedAt: string
}

export interface JoinRequest {
  id: string
  userId: string
  displayName: string
  username?: string
  status: JoinRequestStatus
  createdAt: string
}

export interface FamilyInfo {
  isPersonal?: boolean
  contextName?: string | null
  inviteCode?: string | null
  myRole?: FamilyMemberRole
  members: FamilyMember[]
  joinRequests: JoinRequest[]
}

export interface BudgetContextInfo {
  id: string
  name: string
  isPersonal: boolean
  periodStartDay: number
  baseCurrency: string
  inviteCode?: string
}

export interface UserProfile {
  id: string
  displayName: string
  username?: string
  avatarUrl?: string
  activeContextId?: string
  activeContextName?: string
}

export interface AppSettings {
  baseCurrency: string
  periodStartDay: number
  contextName: string
  isPersonal: boolean
  myRole?: FamilyMemberRole
  canManagePlan?: boolean
  canManageFamilySettings?: boolean
  categories?: { id: string; name: string; kind?: string }[]
}
