/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string
  readonly VITE_TELEGRAM_BOT_USERNAME?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

interface TelegramWebApp {
  initData: string
  ready?: () => void
  expand?: () => void
}

interface TelegramNamespace {
  WebApp?: TelegramWebApp
}

interface Window {
  Telegram?: TelegramNamespace
}
