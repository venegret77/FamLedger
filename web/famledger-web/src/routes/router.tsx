import { createBrowserRouter, Navigate } from 'react-router-dom'
import { AuthGuard, GuestGuard } from '../components/auth/AuthGuard'
import { AppLayout } from '../components/layout/AppLayout'
import { DashboardPage } from '../pages/DashboardPage'
import { DebtsPage } from '../pages/DebtsPage'
import { FamilyPage } from '../pages/FamilyPage'
import { LoginPage } from '../pages/LoginPage'
import { PlanPage } from '../pages/PlanPage'
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
          { path: 'debts', element: <DebtsPage /> },
          { path: 'transactions', element: <TransactionsPage /> },
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
