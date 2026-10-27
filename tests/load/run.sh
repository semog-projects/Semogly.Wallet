#!/usr/bin/env bash
# Roda o teste de carga (k6 em container) contra o ambiente local, com o AppHost já rodando.
# As senhas de administração do Keycloak e do RabbitMQ vêm dos user-secrets do AppHost (gerados pelo Aspire).
#
# Uso: tests/load/run.sh [opções do k6], por exemplo: tests/load/run.sh -e DURATION=5m -e TRANSFER_RATE=4
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
apphost="$here/../../src/Aspire/Semogly.Wallet.AppHost"
secrets="$(dotnet user-secrets list --project "$apphost")"
secret() { sed -n "s/^$1 = //p" <<<"$secrets"; }

# Docker Desktop no Windows (Git Bash) precisa do caminho no formato do Windows para o volume
volume="$(cygpath -w "$here" 2>/dev/null || echo "$here")"
mkdir -p "$here/results"
report="results/$(date +%Y%m%d-%H%M%S).txt"

# Sem -e aqui: com threshold violado o k6 sai com erro, e o relatório ainda precisa ser salvo e informado
set +e
MSYS_NO_PATHCONV=1 docker run --rm \
  --add-host host.docker.internal:host-gateway \
  -v "$volume:/scripts" \
  -e KEYCLOAK_ADMIN_PASSWORD="$(secret 'Parameters:keycloak-password')" \
  -e RABBITMQ_PASSWORD="$(secret 'Parameters:rabbitmq-password')" \
  -e RABBITMQ_URL="http://host.docker.internal:$(secret 'Resources:rabbitmq:management:port')" \
  grafana/k6:2.2.0 run "$@" /scripts/wallet-load.js | tee "$here/$report"
status=${PIPESTATUS[0]}

echo "Relatório salvo em tests/load/$report"
exit "$status"
