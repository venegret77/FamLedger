import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'

type ToastTone = 'warning' | 'info'

type ToastItem = {
  id: number
  title: string
  message?: string
  tone: ToastTone
}

type ToastOptions = {
  title: string
  message?: string
  tone?: ToastTone
  durationMs?: number
}

type ToastContextValue = {
  showToast: (options: ToastOptions) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([])
  const nextId = useRef(1)

  const dismiss = useCallback((id: number) => {
    setItems((current) => current.filter((item) => item.id !== id))
  }, [])

  const showToast = useCallback(
    (options: ToastOptions) => {
      const id = nextId.current++
      const item: ToastItem = {
        id,
        title: options.title,
        message: options.message,
        tone: options.tone ?? 'info',
      }
      setItems((current) => [...current, item])
      window.setTimeout(() => dismiss(id), options.durationMs ?? 6000)
    },
    [dismiss],
  )

  const value = useMemo(() => ({ showToast }), [showToast])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="pointer-events-none fixed inset-x-0 bottom-4 z-[60] flex flex-col items-center gap-2 px-4">
        {items.map((item) => (
          <div
            key={item.id}
            className={
              item.tone === 'warning'
                ? 'pointer-events-auto w-full max-w-md rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-amber-950 shadow-lg'
                : 'pointer-events-auto w-full max-w-md rounded-2xl border border-slate-200 bg-white px-4 py-3 text-slate-900 shadow-lg'
            }
            role="status"
          >
            <p className="text-sm font-semibold">{item.title}</p>
            {item.message && (
              <p className="mt-1 whitespace-pre-line text-sm leading-5 opacity-90">
                {item.message}
              </p>
            )}
            <button
              type="button"
              className="mt-2 text-xs font-medium underline-offset-2 hover:underline"
              onClick={() => dismiss(item.id)}
            >
              Закрыть
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast() {
  const context = useContext(ToastContext)
  if (!context) {
    throw new Error('useToast must be used within ToastProvider')
  }
  return context
}
