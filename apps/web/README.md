# Semogly Wallet · Backoffice (web)

Backoffice dos operadores da carteira: React 19 + TypeScript + Vite, com TanStack Query, React Router,
Tailwind + shadcn/ui e clientes tipados gerados a partir do OpenAPI do Gateway.

## Rodando

O app sobe junto com o restante pelo AppHost do Aspire (`dotnet run --project src/Aspire/Semogly.Wallet.AppHost`),
com `npm install` automático. O dev server do Vite faz proxy de `/api`, `/bff` e `/hubs` para o Gateway: navegador e
Gateway ficam na mesma origem e a sessão é o cookie `HttpOnly` do BFF, sem token no navegador.

## Scripts

| Script | O que faz |
|---|---|
| `npm run dev` | Dev server (o Aspire informa o Gateway via `services__gateway__https__0`) |
| `npm run api:generate` | Regenera `src/api/schema/*.ts` a partir do OpenAPI do Gateway (AppHost rodando) |
| `npm run lint` | oxlint |
| `npx vitest run` | Testes (Vitest + Testing Library) |
| `npm run build` | Typecheck + build de produção |
