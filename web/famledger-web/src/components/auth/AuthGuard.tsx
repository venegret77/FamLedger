import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useMe } from '../../api/hooks'
import { ApiError } from '../../api/client'
import { Spinner } from '../ui/Tabs'

function useAuthState() {
  const query = useMe()
  const unauthorized =
    query.isError &&
    (query.error instanceof ApiError ? query.error.status === 401 : true)

  return {
    ...query,
    isAuthenticated: !!query.data && !unauthorized,
    isChecking: query.isLoading || query.isFetching,
  }
}

export function AuthGuard() {
  const location = useLocation()
  const { isAuthenticated, isChecking } = useAuthState()

  if (isChecking) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50">
        <Spinner />
        <p className="text-sm text-slate-500">Проверяем вход…</p>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}

export function GuestGuard() {
  const { isAuthenticated, isChecking } = useAuthState()

  if (isChecking) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <Spinner />
      </div>
    )
  }

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
