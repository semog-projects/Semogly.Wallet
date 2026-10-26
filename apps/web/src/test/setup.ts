import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// Nenhum teste abre conexão SignalR real (ver signalr-mock.ts)
vi.mock('@microsoft/signalr', () => import('./signalr-mock'))

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})
