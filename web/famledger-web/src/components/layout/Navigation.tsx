import { NavLink } from 'react-router-dom'

interface NavItem {
  to: string
  label: string
  icon: React.ReactNode
  end?: boolean
}

const navItems: NavItem[] = [
  {
    to: '/',
    label: 'Главная',
    end: true,
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <path d="M3 10.5 12 3l9 7.5" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M5 9.5V20a1 1 0 0 0 1 1h4v-6h4v6h4a1 1 0 0 0 1-1V9.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    ),
  },
  {
    to: '/plan',
    label: 'План',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <rect x="3" y="4" width="18" height="18" rx="2" />
        <path d="M16 2v4M8 2v4M3 10h18" strokeLinecap="round" />
      </svg>
    ),
  },
  {
    to: '/reconcile',
    label: 'Сверка',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <path d="M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2" strokeLinecap="round" />
        <rect x="9" y="3" width="6" height="4" rx="1" />
        <path d="M9 12h6M9 16h4" strokeLinecap="round" />
      </svg>
    ),
  },
  {
    to: '/transactions',
    label: 'Операции',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <path d="M4 6h16M4 12h16M4 18h10" strokeLinecap="round" />
      </svg>
    ),
  },
  {
    to: '/history',
    label: 'История',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M3 3v5h5M12 7v5l3 2" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    ),
  },
  {
    to: '/savings',
    label: 'Копилка',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <path
          d="M19 11a7 7 0 1 1-12.8-3.9L5 5h3l1.2 1.5A7 7 0 0 1 19 11Z"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <path d="M16 11h.01M3 14h2M20.5 15.5 22 17" strokeLinecap="round" />
      </svg>
    ),
  },
  {
    to: '/debts',
    label: 'Долги',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    ),
  },
  {
    to: '/reminders',
    label: 'Напоминания',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v5l3 2" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    ),
  },
  {
    to: '/family',
    label: 'Семья',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" strokeLinecap="round" />
        <circle cx="9" cy="7" r="4" />
        <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" strokeLinecap="round" />
      </svg>
    ),
  },
  {
    to: '/settings',
    label: 'Настройки',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
        <circle cx="12" cy="12" r="3" />
        <path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42" strokeLinecap="round" />
      </svg>
    ),
  },
]

export const primaryMobileNav = navItems.filter((item) =>
  ['/', '/plan', '/transactions', '/family'].includes(item.to),
)

export { navItems }

function NavLinkItem({ item, compact = false }: { item: NavItem; compact?: boolean }) {
  return (
    <NavLink
      to={item.to}
      end={item.end}
      className={({ isActive }) =>
        compact
          ? `flex flex-1 flex-col items-center gap-1 px-2 py-2 text-[11px] font-medium transition-colors ${
              isActive ? 'text-brand-600' : 'text-slate-500 hover:text-slate-700'
            }`
          : `flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors ${
              isActive
                ? 'bg-brand-50 text-brand-700'
                : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
            }`
      }
    >
      {item.icon}
      <span className={compact ? 'leading-none' : undefined}>{item.label}</span>
    </NavLink>
  )
}

export function Sidebar() {
  return (
    <aside className="hidden lg:flex lg:w-64 lg:flex-col lg:border-r lg:border-slate-200 lg:bg-white">
      <div className="flex h-16 items-center gap-2 border-b border-slate-200 px-6">
        <div className="flex size-9 items-center justify-center rounded-xl bg-brand-600 text-sm font-bold text-white">
          FL
        </div>
        <div>
          <p className="text-sm font-bold text-slate-900">FamLedger</p>
          <p className="text-xs text-slate-500">Семейный бюджет</p>
        </div>
      </div>
      <nav className="flex flex-1 flex-col gap-1 p-4">
        {navItems.map((item) => (
          <NavLinkItem key={item.to} item={item} />
        ))}
      </nav>
    </aside>
  )
}

export function BottomNav() {
  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-slate-200 bg-white/95 backdrop-blur lg:hidden safe-bottom">
      <div className="mx-auto flex max-w-lg items-stretch justify-around px-1">
        {primaryMobileNav.map((item) => (
          <NavLinkItem key={item.to} item={item} compact />
        ))}
        <NavLink
          to="/settings"
          className={({ isActive }) =>
            `flex flex-1 flex-col items-center gap-1 px-2 py-2 text-[11px] font-medium transition-colors ${
              isActive ? 'text-brand-600' : 'text-slate-500 hover:text-slate-700'
            }`
          }
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="size-5">
            <circle cx="12" cy="12" r="1.5" fill="currentColor" stroke="none" />
            <circle cx="6" cy="12" r="1.5" fill="currentColor" stroke="none" />
            <circle cx="18" cy="12" r="1.5" fill="currentColor" stroke="none" />
          </svg>
          <span className="leading-none">Ещё</span>
        </NavLink>
      </div>
    </nav>
  )
}

export function MobileMoreMenu() {
  const secondaryItems = navItems.filter(
    (item) => !primaryMobileNav.some((p) => p.to === item.to) && item.to !== '/settings',
  )

  if (secondaryItems.length === 0) return null

  return (
    <div className="grid grid-cols-2 gap-2 lg:hidden">
      {secondaryItems.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm"
        >
          {item.icon}
          {item.label}
        </NavLink>
      ))}
    </div>
  )
}
