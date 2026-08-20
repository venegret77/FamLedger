import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../api/hooks'
import { Card, CardDescription, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'

const API_URL =
  import.meta.env.VITE_API_URL === ''
    ? ''
    : (import.meta.env.VITE_API_URL?.trim() || 'http://localhost:8080')

const BOT_USERNAME = (import.meta.env.VITE_TELEGRAM_BOT_USERNAME?.trim() ?? '').replace(/^@/, '')

type TelegramWidgetUser = {
  id: number
  first_name?: string
  last_name?: string
  username?: string
  photo_url?: string
  auth_date: number
  hash: string
}

declare global {
  interface Window {
    onTelegramAuth?: (user: TelegramWidgetUser) => void
  }
}

async function exchangeBotToken(
  token: string,
  queryClient: ReturnType<typeof useQueryClient>,
): Promise<void> {
  const res = await fetch(`${API_URL}/api/auth/bot`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ token }),
  })
  if (!res.ok) {
    let message = `Ошибка ${res.status}`
    try {
      const body = (await res.json()) as { message?: string }
      if (body.message) message = body.message
    } catch {
      const text = await res.text()
      if (text) message = text
    }
    throw new Error(message)
  }
  await queryClient.invalidateQueries({ queryKey: queryKeys.me })
}

async function exchangeTelegramWidget(
  user: TelegramWidgetUser,
  queryClient: ReturnType<typeof useQueryClient>,
): Promise<void> {
  const res = await fetch(`${API_URL}/api/auth/telegram`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({
      id: user.id,
      firstName: user.first_name,
      lastName: user.last_name,
      username: user.username,
      photoUrl: user.photo_url,
      authDate: user.auth_date,
      hash: user.hash,
    }),
  })
  if (!res.ok) {
    let message = `Ошибка ${res.status}`
    try {
      const body = (await res.json()) as { message?: string }
      if (body.message) message = body.message
    } catch {
      /* ignore */
    }
    throw new Error(message)
  }
  await queryClient.invalidateQueries({ queryKey: queryKeys.me })
}

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const widgetHostRef = useRef<HTMLDivElement>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showCodeFallback, setShowCodeFallback] = useState(false)
  const [manualToken, setManualToken] = useState('')

  const from = (location.state as { from?: string } | null)?.from ?? '/'
  const botLoginUrl = BOT_USERNAME ? `https://t.me/${BOT_USERNAME}?start=login` : null

  const finish = async (action: () => Promise<void>) => {
    setLoading(true)
    setError(null)
    try {
      await action()
      navigate(from, { replace: true })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Не удалось войти')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!BOT_USERNAME || !widgetHostRef.current) return

    window.onTelegramAuth = (user) => {
      void finish(() => exchangeTelegramWidget(user, queryClient))
    }

    const host = widgetHostRef.current
    host.innerHTML = ''
    const script = document.createElement('script')
    script.src = 'https://telegram.org/js/telegram-widget.js?22'
    script.async = true
    script.setAttribute('data-telegram-login', BOT_USERNAME)
    script.setAttribute('data-size', 'large')
    script.setAttribute('data-radius', '12')
    script.setAttribute('data-onauth', 'onTelegramAuth(user)')
    script.setAttribute('data-request-access', 'write')
    host.appendChild(script)

    return () => {
      delete window.onTelegramAuth
      host.innerHTML = ''
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- mount once per bot username
  }, [BOT_USERNAME, queryClient])

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-brand-50 via-white to-slate-100 px-4 py-12">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex size-16 items-center justify-center rounded-2xl bg-brand-600 text-xl font-bold text-white shadow-lg shadow-brand-600/30">
            FL
          </div>
          <h1 className="text-3xl font-bold tracking-tight text-slate-900">FamLedger</h1>
          <p className="mt-2 text-slate-500">Войдите, чтобы вести бюджет</p>
        </div>

        <Card className="shadow-md">
          <CardTitle>Вход</CardTitle>
          <CardDescription>Через Telegram — одним нажатием.</CardDescription>

          <div className="mt-6 space-y-4">
            {BOT_USERNAME ? (
              <>
                <div className="flex min-h-[44px] justify-center" ref={widgetHostRef} />

                {loading && (
                  <p className="text-center text-sm text-slate-500">Входим…</p>
                )}

                <button
                  type="button"
                  className="w-full text-center text-sm text-slate-500 underline-offset-2 hover:text-slate-700 hover:underline"
                  onClick={() => setShowCodeFallback((v) => !v)}
                >
                  {showCodeFallback ? 'Скрыть вход по коду' : 'Войти кодом из бота'}
                </button>

                {showCodeFallback && (
                  <div className="space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-4">
                    {botLoginUrl && (
                      <Button
                        variant="secondary"
                        size="sm"
                        className="w-full"
                        onClick={() => window.open(botLoginUrl, '_blank', 'noopener,noreferrer')}
                      >
                        <TelegramIcon />
                        Открыть бота (/start login)
                      </Button>
                    )}
                    <form
                      className="flex gap-2"
                      onSubmit={(e) => {
                        e.preventDefault()
                        const token = manualToken.trim()
                        if (token) void finish(() => exchangeBotToken(token, queryClient))
                      }}
                    >
                      <input
                        type="text"
                        value={manualToken}
                        onChange={(e) => setManualToken(e.target.value)}
                        placeholder="Код из бота"
                        className="min-w-0 flex-1 rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20"
                        disabled={loading}
                        autoComplete="off"
                        spellCheck={false}
                      />
                      <Button
                        type="submit"
                        variant="secondary"
                        disabled={loading || !manualToken.trim()}
                      >
                        Войти
                      </Button>
                    </form>
                  </div>
                )}
              </>
            ) : (
              <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                <p className="font-medium">Бот не настроен</p>
                <p className="mt-1 text-amber-800/90">
                  Укажите <code className="rounded bg-amber-100 px-1">TELEGRAM_BOT_USERNAME</code> в{' '}
                  <code className="rounded bg-amber-100 px-1">.env</code> и пересоберите web.
                </p>
              </div>
            )}

            {error && (
              <p className="rounded-lg bg-red-50 px-3 py-2 text-center text-sm text-red-700">
                {error}
              </p>
            )}
          </div>
        </Card>
      </div>
    </div>
  )
}

function TelegramIcon() {
  return (
    <svg viewBox="0 0 24 24" className="size-5" fill="currentColor" aria-hidden>
      <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.64 6.8c-.15 1.58-.8 5.42-1.13 7.19-.14.75-.42 1-.68 1.03-.58.05-1.02-.38-1.58-.75-.88-.58-1.38-.94-2.23-1.5-.99-.65-.35-1.01.22-1.59.15-.15 2.71-2.48 2.76-2.69a.2.2 0 0 0-.05-.18c-.06-.05-.14-.03-.21-.02-.09.02-1.49.95-4.22 2.79-.4.27-.76.41-1.08.4-.36-.01-1.04-.2-1.55-.37-.63-.2-1.12-.31-1.08-.66.02-.18.27-.36.74-.55 2.92-1.27 4.86-2.11 5.83-2.51 2.78-1.16 3.35-1.36 3.73-1.36.08 0 .27.02.39.12.1.08.13.19.14.27-.01.06.01.24 0 .38z" />
    </svg>
  )
}
