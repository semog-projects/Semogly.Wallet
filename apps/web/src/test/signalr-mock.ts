// Substitui @microsoft/signalr nos testes: nenhuma conexão real e eventos do hub emitidos pelo teste.

type Handler = (...args: unknown[]) => void

class FakeHubConnection {
  state = 'Disconnected'
  readonly handlers = new Map<string, Handler>()

  on(event: string, handler: Handler) {
    this.handlers.set(event, handler)
  }

  onreconnecting() {}
  onreconnected() {}
  onclose() {}

  async start() {
    this.state = 'Connected'
    hub.connection = this
  }

  async stop() {
    this.state = 'Disconnected'
  }
}

export class HubConnectionBuilder {
  withUrl(url: string) {
    hub.url = url
    return this
  }

  withAutomaticReconnect() {
    return this
  }

  configureLogging() {
    return this
  }

  build() {
    return new FakeHubConnection()
  }
}

export const HubConnectionState = { Disconnected: 'Disconnected', Connected: 'Connected' }
export const LogLevel = { Warning: 3 }

/** Controle do hub falso: URL usada e emissão de eventos do servidor. */
export const hub = {
  url: undefined as string | undefined,
  connection: undefined as FakeHubConnection | undefined,
  emit(event: string, payload: unknown) {
    hub.connection?.handlers.get(event)?.(payload)
  },
}
