// Teste de carga da carteira pelo Gateway (k6), contra o ambiente local do AppHost.
//
// Cenários simultâneos:
//   - onboarding: clientes novos abrem conta e consultam /me
//   - deposits:   o operador registra depósitos (com Idempotency-Key)
//   - transfers:  transferências entre titulares, acompanhadas até o desfecho (latência ponta a ponta da saga)
//   - reads:      titulares consultam extrato, limite diário e notificações
//
// Depois da carga (teardown), verifica os invariantes: nenhuma transferência pendente, livro balanceado com
// Clearing zerado, saldo do Accounts igual ao do Ledger em todas as contas e nenhuma mensagem nova nas filas de erro.
//
// A taxa respeita os limites do próprio sistema, para medir o sistema e não os limites: o Gateway aceita 10
// transferências por minuto por usuário e o antifraude recusa mais de 5 por minuto por conta. As transferências
// giram entre os titulares (round-robin): com 60 titulares a 3/s, cada conta faz 3 por minuto.

import http from 'k6/http';
import encoding from 'k6/encoding';
import exec from 'k6/execution';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const GATEWAY = __ENV.GATEWAY_URL || 'http://host.docker.internal:5100';
const KEYCLOAK = __ENV.KEYCLOAK_URL || 'http://host.docker.internal:8080';
const RABBITMQ = __ENV.RABBITMQ_URL || 'http://host.docker.internal:15672';
const REALM = 'semogly-wallet';
const PASSWORD = 'Carga@123';

const TRADERS = Number(__ENV.TRADERS || 60);
const NEWCOMERS = Number(__ENV.NEWCOMERS || 20);
const DURATION = __ENV.DURATION || '2m';
const TRANSFER_RATE = Number(__ENV.TRANSFER_RATE || 3);
const READ_RATE = Number(__ENV.READ_RATE || 4);
const INITIAL_BALANCE = 1000;

// Operadores da execução: o Gateway limita 100 requisições/min por usuário, então os depósitos iniciais se dividem
// entre vários (até 50 cada) e um operador dedicado atende os depósitos e as verificações durante a carga
const SETUP_OPERATORS = Math.ceil(TRADERS / 50);

// Identifica a execução: usuários e chaves não colidem com execuções anteriores
const RUN = __ENV.RUN_ID || `${Date.now()}`;

const transferDuration = new Trend('transfer_e2e_duration', true);
const transfersCompleted = new Counter('transfers_completed');
const transfersFailed = new Counter('transfers_failed');
const transfersTimedOut = new Counter('transfers_timed_out');
const invariantViolations = new Counter('invariant_violations');

export const options = {
  setupTimeout: '5m',
  teardownTimeout: '5m',
  scenarios: {
    onboarding: {
      executor: 'per-vu-iterations',
      exec: 'onboarding',
      vus: NEWCOMERS,
      iterations: 1,
      maxDuration: DURATION,
    },
    deposits: {
      executor: 'constant-arrival-rate',
      exec: 'deposit',
      rate: 1,
      timeUnit: '2s', // operador único: 30/min, abaixo do limite de 100/min por usuário
      duration: DURATION,
      preAllocatedVUs: 2,
    },
    transfers: {
      executor: 'constant-arrival-rate',
      exec: 'transfer',
      rate: TRANSFER_RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: TRANSFER_RATE * 8, // cada iteração espera o desfecho da saga
      maxVUs: TRANSFER_RATE * 20,
    },
    reads: {
      executor: 'constant-arrival-rate',
      exec: 'read',
      rate: READ_RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: READ_RATE * 2,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{scenario:reads}': ['p(95)<300', 'p(99)<800'],
    'http_req_duration{name:POST /transfers}': ['p(95)<300'],
    transfer_e2e_duration: ['p(95)<2000'],
    transfers_failed: ['count==0'],
    transfers_timed_out: ['count==0'],
    invariant_violations: ['count==0'],
  },
};

// ---------- preparação ----------

export function setup() {
  const startedAt = new Date().toISOString();
  const admin = adminToken();
  const setupOperators = [];
  for (let i = 0; i < SETUP_OPERATORS; i++) {
    setupOperators.push(createUser(admin, `operator-setup-${i}`, 'operator').token);
  }
  const operator = createUser(admin, 'operator', 'operator').token;

  const traders = [];
  for (let i = 0; i < TRADERS; i++) {
    const user = createUser(admin, `trader-${i}`, 'customer');
    const accountId = openAccount(user, `Trader ${i}`);
    traders.push({ ...user, accountId });
  }

  // A conta chega ao Ledger pelo evento AccountCreated (assíncrono): o depósito inicial espera por ela
  traders.forEach((trader, i) => {
    const funded = waitUntil(
      () => depositRequest(
        setupOperators[i % SETUP_OPERATORS], trader.accountId, INITIAL_BALANCE, `setup-${RUN}-${i}`,
        http.expectedStatuses(200, 404)),
      (response) => response.status !== 404,
      30,
      0.5,
    );
    expectStatus(funded, 200, 'depósito inicial');
  });

  const newcomers = [];
  for (let i = 0; i < NEWCOMERS; i++) {
    newcomers.push(createUser(admin, `newcomer-${i}`, 'customer'));
  }

  return { startedAt, operator, traders, newcomers, errorMessagesBefore: errorQueueMessages() };
}

// ---------- cenários ----------

export function onboarding(data) {
  const user = data.newcomers[exec.scenario.iterationInTest];
  openAccount(user, 'Cliente Novo');

  const me = http.get(`${GATEWAY}/api/accounts/me`, auth(user.token, 'GET /accounts/me'));
  check(me, { 'onboarding: conta aparece em /me': (r) => r.status === 200 && r.json().length === 1 });
}

export function deposit(data) {
  const trader = data.traders[exec.scenario.iterationInTest % data.traders.length];
  const key = `run-${RUN}-${exec.scenario.iterationInTest}`;

  // 409 = conflito de concorrência na conta CashIn (outro depósito simultâneo): o cliente repete com a mesma
  // Idempotency-Key, que garante um único lançamento mesmo que a primeira tentativa tenha passado
  const response = waitUntil(
    () => depositRequest(data.operator, trader.accountId, 10, key, http.expectedStatuses(200, 409)),
    (r) => r.status !== 409,
    5,
    0.2,
  );
  check(response, { 'depósito: 200': (r) => r.status === 200 });
}

export function transfer(data) {
  const n = exec.scenario.iterationInTest;
  const source = data.traders[n % data.traders.length];
  const destination = data.traders[(n + 1 + (n % 7)) % data.traders.length];
  // Centavos divididos por 100 (0.01 * 35 daria 0.35000000000000003, recusado pela regra de duas casas)
  const amount = (1 + (n % 100)) / 100;

  const startedAt = Date.now();
  const created = http.post(
    `${GATEWAY}/api/transfers`,
    JSON.stringify({ sourceAccountId: source.accountId, destinationAccountId: destination.accountId, amount }),
    auth(source.token, 'POST /transfers', { 'Idempotency-Key': `${RUN}-${n}` }),
  );
  if (!check(created, { 'transferência: 202': (r) => r.status === 202 })) {
    return;
  }

  // Acompanha a saga até o desfecho (antifraude → débito → crédito). O polling começa em 100 ms (a resolução da
  // latência ponta a ponta medida) e cresce até 1 s, para não estourar o limite por usuário se a saga demorar
  const id = created.json('id');
  for (let interval = 0.1, waited = 0; waited < 30; waited += interval, interval = Math.min(interval * 1.5, 1)) {
    sleep(interval);
    const status = http.get(`${GATEWAY}/api/transfers/${id}`, auth(source.token, 'GET /transfers/{id}')).json('status');

    if (status === 'Completed') {
      transferDuration.add(Date.now() - startedAt);
      transfersCompleted.add(1);
      return;
    }
    if (status === 'Failed') {
      transfersFailed.add(1);
      return;
    }
  }

  transfersTimedOut.add(1);
}

export function read(data) {
  const trader = data.traders[exec.scenario.iterationInTest % data.traders.length];
  const options = (name) => auth(trader.token, name);

  const responses = http.batch([
    ['GET', `${GATEWAY}/api/ledger/accounts/${trader.accountId}/statement?pageSize=10`, null, options('GET /statement')],
    ['GET', `${GATEWAY}/api/fraud/accounts/${trader.accountId}/daily-usage`, null, options('GET /daily-usage')],
    ['GET', `${GATEWAY}/api/notifications/accounts/${trader.accountId}`, null, options('GET /notifications')],
  ]);
  check(responses, { 'leituras: 200': (all) => all.every((r) => r.status === 200) });
}

// ---------- invariantes ----------

export function teardown(data) {
  const pending = waitUntil(
    () => http.get(`${GATEWAY}/api/transfers?status=Pending&from=${data.startedAt}&pageSize=1`, auth(data.operator, 'invariants'))
      .json('totalCount'),
    (count) => count === 0,
  );
  verify(`nenhuma transferência pendente (restantes: ${pending})`, pending === 0);

  const book = http.get(`${GATEWAY}/api/ledger/book`, auth(data.operator, 'invariants')).json();
  verify(`livro balanceado (total ${book.total})`, book.isBalanced);
  verify(`Clearing zerado (${book.clearing})`, Number(book.clearing) === 0);

  // O Accounts converge pelos eventos do Ledger: espera alguns segundos antes de comparar
  const mismatches = waitUntil(
    () => data.traders.filter((t) => accountsBalance(t) !== ledgerBalance(t)).length,
    (count) => count === 0,
  );
  verify(`saldo do Accounts igual ao do Ledger (${mismatches} divergentes)`, mismatches === 0);

  const newErrors = errorQueueMessages() - data.errorMessagesBefore;
  verify(`nenhuma mensagem nova em fila de erro (${newErrors})`, newErrors === 0);
}

function verify(description, ok) {
  check(null, { [`invariante: ${description}`]: () => ok });
  if (!ok) {
    invariantViolations.add(1);
  }
}

function waitUntil(read, done, timeoutSeconds = 60, intervalSeconds = 3) {
  let value = read();
  for (let waited = 0; !done(value) && waited < timeoutSeconds; waited += intervalSeconds) {
    sleep(intervalSeconds);
    value = read();
  }
  return value;
}

function accountsBalance(trader) {
  return Number(http.get(`${GATEWAY}/api/accounts/${trader.accountId}/balance`, auth(trader.token, 'invariants')).json('balance'));
}

function ledgerBalance(trader) {
  return Number(
    http.get(`${GATEWAY}/api/ledger/accounts/${trader.accountId}/statement?pageSize=1`, auth(trader.token, 'invariants'))
      .json('balance'),
  );
}

/** Mensagens paradas nas filas de erro (_error) do RabbitMQ. */
function errorQueueMessages() {
  const response = http.get(`${RABBITMQ}/api/queues`, {
    headers: {
      Authorization: `Basic ${encoding.b64encode(`${__ENV.RABBITMQ_USER || 'guest'}:${__ENV.RABBITMQ_PASSWORD}`)}`,
    },
    tags: { name: 'rabbitmq' },
  });
  expectStatus(response, 200, 'API de gerenciamento do RabbitMQ');
  return response.json()
    .filter((queue) => queue.name.endsWith('_error'))
    .reduce((total, queue) => total + (queue.messages || 0), 0);
}

// ---------- apoio ----------

function auth(token, name, headers = {}) {
  return {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', ...headers },
    tags: { name },
  };
}

function gatewayToken(username, password) {
  const response = http.post(`${GATEWAY}/auth/token`, JSON.stringify({ username, password }), {
    headers: { 'Content-Type': 'application/json' },
    tags: { name: 'setup' },
  });
  expectStatus(response, 200, `token de ${username}`);
  return response.json('accessToken');
}

function adminToken() {
  const response = http.post(`${KEYCLOAK}/realms/master/protocol/openid-connect/token`, {
    grant_type: 'password',
    client_id: 'admin-cli',
    username: __ENV.KEYCLOAK_ADMIN || 'admin',
    password: __ENV.KEYCLOAK_ADMIN_PASSWORD,
  }, { tags: { name: 'setup' } });
  expectStatus(response, 200, 'token de administração do Keycloak');
  return response.json('access_token');
}

/** Usuário no realm com o papel informado; o nome de usuário é o e-mail, como nos usuários de demonstração. */
function createUser(admin, name, roleName) {
  const email = `carga-${RUN}-${name}@semogly.dev`;
  const headers = { headers: { Authorization: `Bearer ${admin}`, 'Content-Type': 'application/json' }, tags: { name: 'setup' } };

  const created = http.post(`${KEYCLOAK}/admin/realms/${REALM}/users`, JSON.stringify({
    username: email,
    email,
    emailVerified: true,
    enabled: true,
    firstName: 'Carga',
    lastName: name,
    credentials: [{ type: 'password', value: PASSWORD, temporary: false }],
  }), headers);
  expectStatus(created, 201, `usuário ${email}`);

  const userId = created.headers.Location.split('/').pop();
  const role = http.get(`${KEYCLOAK}/admin/realms/${REALM}/roles/${roleName}`, headers).json();
  expectStatus(
    http.post(`${KEYCLOAK}/admin/realms/${REALM}/users/${userId}/role-mappings/realm`, JSON.stringify([role]), headers),
    204,
    `papel ${roleName}`,
  );

  return { email, token: gatewayToken(email, PASSWORD) };
}

function openAccount(user, ownerName) {
  const response = http.post(
    `${GATEWAY}/api/accounts`,
    JSON.stringify({ ownerName, document: randomCpf(), email: user.email }),
    auth(user.token, 'POST /accounts'),
  );
  check(response, { 'abertura de conta: 201': (r) => r.status === 201 });
  return response.json('id');
}

function depositRequest(operatorToken, accountId, amount, idempotencyKey, responseCallback = undefined) {
  const params = auth(operatorToken, 'POST /deposits', { 'Idempotency-Key': idempotencyKey });
  return http.post(
    `${GATEWAY}/api/ledger/accounts/${accountId}/deposits`,
    JSON.stringify({ amount }),
    responseCallback ? { ...params, responseCallback } : params,
  );
}

function expectStatus(response, status, what) {
  if (response.status !== status) {
    exec.test.abort(`${what}: esperado ${status}, recebido ${response.status} ${response.body}`);
  }
  return response;
}

function randomCpf() {
  let digits = '';
  for (let i = 0; i < 11; i++) {
    digits += Math.floor(Math.random() * 10);
  }
  return digits;
}
