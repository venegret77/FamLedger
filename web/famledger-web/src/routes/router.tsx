import { createBrowserRouter, Navigate } from 'react-router-dom'
import { AuthGuard, GuestGuard } from '../components/auth/AuthGuard'
import { AppLayout } from '../components/layout/AppLayout'
import { DashboardPage } from '../pages/DashboardPage'
import { LoginPage } from '../pages/LoginPage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <GuestGuard />,
    children: [{ index: true, element: <LoginPage /> }],
  },
  {
    element: <AuthGuard />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: 'plan', lazy: async () => ({ Component: (await import('../pages/PlanPage')).PlanPage }) },
          { path: 'reconcile', lazy: async () => ({ Component: (await import('../pages/ReconcilePage')).ReconcilePage }) },
          { path: 'savings', lazy: async () => ({ Component: (await import('../pages/SavingsPage')).SavingsPage }) },
          { path: 'debts', lazy: async () => ({ Component: (await import('../pages/DebtsPage')).DebtsPage }) },
          { path: 'transactions', lazy: async () => ({ Component: (await import('../pages/TransactionsPage')).TransactionsPage }) },
          { path: 'history', lazy: async () => ({ Component: (await import('../pages/HistoryPage')).HistoryPage }) },
          { path: 'reminders', lazy: async () => ({ Component: (await import('../pages/RemindersPage')).RemindersPage }) },
          { path: 'family', lazy: async () => ({ Component: (await import('../pages/FamilyPage')).FamilyPage }) },
          { path: 'settings', lazy: async () => ({ Component: (await import('../pages/SettingsPage')).SettingsPage }) },
        ],
      },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/" replace />,
  },
])
