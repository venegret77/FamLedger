import { Outlet } from 'react-router-dom'
import { useMe } from '../../api/hooks'
import { BottomNav, Sidebar } from './Navigation'

export function AppLayout() {
  const { data: user } = useMe()

  return (
    <div className="flex min-h-full bg-slate-50">
      <Sidebar />
      <div className="flex min-h-full flex-1 flex-col">
        <header className="sticky top-0 z-30 border-b border-slate-200/80 bg-white/90 backdrop-blur safe-top lg:hidden">
          <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
            <div className="flex items-center gap-2">
              <div className="flex size-8 items-center justify-center rounded-lg bg-brand-600 text-xs font-bold text-white">
                FL
              </div>
              <div>
                <p className="text-sm font-semibold text-slate-900">FamLedger</p>
                {user?.activeContextName && (
                  <p className="text-xs text-slate-500">{user.activeContextName}</p>
                )}
              </div>
            </div>
          </div>
        </header>

        <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-5 pb-24 lg:px-8 lg:py-8 lg:pb-8">
          <Outlet />
        </main>

        <BottomNav />
      </div>
    </div>
  )
}
