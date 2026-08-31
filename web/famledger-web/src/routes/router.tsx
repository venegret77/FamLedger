import { createBrowserRouter, Navigate } from 'react-router-dom'
import { AuthGuard, GuestGuard } from '../components/auth/AuthGuard'
import { AppLayout } from '../components/layout/AppLayout'
import { DashboardPage } from '../pages/DashboardPage'
import { DebtsPage } from '../pages/DebtsPage'
import { FamilyPage } from '../pages/FamilyPage'
import { HistoryPage } from '../pages/HistoryPage'
import { LoginPage } from '../pages/LoginPage'
import { PlanPage } from '../pages/PlanPage'
import { ReconcilePage } from '../pages/ReconcilePage'
import { RemindersPage } from '../pages/RemindersPage'
import { SavingsPage } from '../pages/SavingsPage'
import { SettingsPage } from '../pages/SettingsPage'
import { TransactionsPage } from '../pages/TransactionsPage'

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
          { path: 'plan', element: <PlanPage /> },
          { path: 'reconcile', element: <ReconcilePage /> },
          { path: 'savings', element: <SavingsPage /> },
          { path: 'debts', element: <DebtsPage /> },
          { path: 'transactions', element: <TransactionsPage /> },
          { path: 'history', element: <HistoryPage /> },
          { path: 'reminders', element: <RemindersPage /> },
          { path: 'family', element: <FamilyPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/" replace />,
  },
])
