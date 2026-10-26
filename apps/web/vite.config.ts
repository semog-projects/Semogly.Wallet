import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Endereço do Gateway injetado pelo Aspire (WithReference); o fallback permite rodar o front sozinho
const gateway =
  process.env.services__gateway__https__0 ?? process.env.services__gateway__http__0 ?? 'https://localhost:7100'

// O Gateway usa o certificado de desenvolvimento do .NET, que o proxy do Vite não conhece
const toGateway = { target: gateway, changeOrigin: true, secure: false }

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    // Mesma origem para o navegador: o cookie SameSite=Strict do BFF funciona sem CORS
    proxy: {
      '/api': toGateway,
      '/bff': toGateway,
      '/hubs': { ...toGateway, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: false,
  },
})
