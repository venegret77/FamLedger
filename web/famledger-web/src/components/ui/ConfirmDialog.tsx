import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { Button } from './Button'

type ConfirmOptions = {
  title: string
  message?: string
  confirmText?: string
  cancelText?: string
  variant?: 'danger' | 'primary'
}

type ConfirmContextValue = {
  confirm: (options: ConfirmOptions) => Promise<boolean>
}

const ConfirmContext = createContext<ConfirmContextValue | null>(null)

type DialogState = ConfirmOptions & {
  resolve: (value: boolean) => void
}

export function ConfirmDialogProvider({ children }: { children: ReactNode }) {
  const [dialog, setDialog] = useState<DialogState | null>(null)

  const confirm = useCallback((options: ConfirmOptions) => {
    return new Promise<boolean>((resolve) => {
      setDialog({
        confirmText: 'Удалить',
        cancelText: 'Отмена',
        variant: 'danger',
        ...options,
        resolve,
      })
    })
  }, [])

  const closeDialog = useCallback((result: boolean) => {
    setDialog((current) => {
      current?.resolve(result)
      return null
    })
  }, [])

  const value = useMemo(() => ({ confirm }), [confirm])

  return (
    <ConfirmContext.Provider value={value}>
      {children}
      {dialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 px-4">
          <div className="w-full max-w-md rounded-3xl bg-white p-6 shadow-2xl">
            <h2 className="text-lg font-semibold text-slate-900">{dialog.title}</h2>
            {dialog.message && (
              <p className="mt-2 text-sm leading-6 text-slate-600">{dialog.message}</p>
            )}
            <div className="mt-6 flex justify-end gap-3">
              <Button variant="secondary" onClick={() => closeDialog(false)}>
                {dialog.cancelText}
              </Button>
              <Button
                variant={dialog.variant === 'primary' ? 'primary' : 'danger'}
                onClick={() => closeDialog(true)}
              >
                {dialog.confirmText}
              </Button>
            </div>
          </div>
        </div>
      )}
    </ConfirmContext.Provider>
  )
}

export function useConfirmDialog() {
  const context = useContext(ConfirmContext)
  if (!context) {
    throw new Error('useConfirmDialog must be used within ConfirmDialogProvider')
  }
  return context
}
