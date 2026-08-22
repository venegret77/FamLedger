import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { ApiError } from './api/client'
import { ConfirmDialogProvider } from './components/ui/ConfirmDialog'
import { ToastProvider } from './components/ui/Toast'
import { router } from './routes/router'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (count, error) => {
        if (error instanceof ApiError && error.status === 401) return false
        return count < 1
      },
      refetchOnWindowFocus: true,
    },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ConfirmDialogProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </ConfirmDialogProvider>
    </QueryClientProvider>
  )
}
