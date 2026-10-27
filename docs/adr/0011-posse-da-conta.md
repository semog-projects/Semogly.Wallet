# ADR 0011: Posse da conta e validação do JWT em todos os serviços

**Status:** aceita (resolve as pendências de segurança da [ADR 0008](0008-gateway-yarp-jwt.md))

## Contexto

O Gateway exigia um JWT válido, mas qualquer cliente autenticado conseguia ler o extrato, o saldo, as notificações e
o limite de outra conta, ou transferir a partir dela, bastando conhecer o id. Além disso, só o Accounts validava o
token: os demais serviços confiavam em tudo que chegasse pela rede interna.

## Decisão

- **JWT em todos os serviços** (defesa em profundidade): `AddJwtValidation` (ServiceDefaults) em Accounts,
  Transfers, Ledger, Fraud e Notifications, com a política `operator` para as rotas do backoffice (busca de contas,
  listagem de transferências, livro-razão e depósitos). O Gateway continua aplicando as mesmas políticas na borda.
- **Regra de posse:** o titular é o usuário cujo e-mail do token é o e-mail da conta; o papel `operator` acessa
  todas as contas. A regra é aplicada pelo Accounts, dono do vínculo conta → titular.
- **Consulta de posse nos demais serviços** (`IAccountAccess`): o serviço chama `GET /accounts/{id}` no Accounts
  repassando o token do próprio usuário. 200 libera; 403 ou 404 nega. A resposta positiva fica 10 minutos em
  cache (a conta não troca de titular); a negativa não, porque a conta pode ser aberta logo depois. Operadores não
  geram consulta.
- **Onde se aplica:**
  - Rotas por conta (extrato, limite diário, notificações, dados e saldo): filtro `RequireAccountAccess()`, que
    usa o parâmetro de rota `{accountId}`.
  - `POST /transfers`: a conta de origem precisa ser do usuário.
  - `GET /transfers/{id}`: titular da origem ou do destino (pagador e recebedor acompanham a transferência).
  - `POST /accounts`: cliente só abre conta no próprio e-mail.
  - Hub SignalR: `SubscribeToAccount` só para contas do usuário (o token vem da query string no app ou da sessão
    do BFF no web).
- **Resposta:** 403 com ProblemDetails (`Account.Forbidden`) para conta de outro titular; 401 sem token.

## Alternativas consideradas

- **Projeção local de titulares** em cada serviço, alimentada pelo evento `AccountCreated`: sem chamada síncrona,
  mas duplica a regra em cinco lugares, exige uma tabela (ou chave no Redis) por serviço e não cobre as contas
  abertas antes da projeção existir sem uma carga inicial.
- **Lista de contas no token** (claim `accounts`): o token ficaria desatualizado a cada conta aberta e cresceria
  com o número de contas.
- **Só o Gateway verificar a posse:** contraria a defesa em profundidade; qualquer chamada interna escaparia da regra.

## Consequências

- **Positivas:**
  - Cliente só vê e movimenta as próprias contas, verificado em cada serviço, não só na borda.
  - Uma única regra de posse, mantida pelo dono do dado; os demais serviços não guardam dados de titular.
  - Testes de integração cobrem 401, 403 e acesso do titular em cada serviço e no hub.
- **Negativas:**
  - Chamada síncrona ao Accounts na primeira consulta de cada conta por usuário (depois, cache). Se o Accounts
    estiver fora, clientes recebem erro nessas rotas; operadores não são afetados.
  - O cache é por instância (memória); com muitas instâncias, cada uma faz a própria primeira consulta.
  - A posse é pelo e-mail: trocar o e-mail no Keycloak sem trocar na conta corta o acesso do titular.
