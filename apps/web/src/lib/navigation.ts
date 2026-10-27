/** Navegação para fora do SPA (login no Keycloak); isolada para ser substituída nos testes. */
export function redirectTo(url: string): void {
  window.location.assign(url)
}
