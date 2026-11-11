import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './index.css'

window.addEventListener('vite:preloadError', (event) => {
  const key = 'famledger:reloaded-build'
  try {
    if (sessionStorage.getItem(key) === import.meta.url) return
    sessionStorage.setItem(key, import.meta.url)
  } catch {
    return
  }
  event.preventDefault()
  window.location.reload()
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
