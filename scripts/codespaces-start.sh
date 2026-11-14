#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -f .env ]]; then
  cp .env.example .env
  echo "Created .env from .env.example"
fi

echo "Waiting for Docker..."
for _ in $(seq 1 60); do
  if docker info >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not available. Rebuild the Codespace container and try again."
  exit 1
fi

if [[ -n "${CODESPACE_NAME:-}" && -n "${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-}" ]]; then
  export EXTERNAL_TOOLS_GRAFANA_URL="https://${CODESPACE_NAME}-3000.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"
  export EXTERNAL_TOOLS_DOZZLE_URL="https://${CODESPACE_NAME}-8888.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"
  export EXTERNAL_TOOLS_RABBITMQ_URL="https://${CODESPACE_NAME}-15672.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"
  export EXTERNAL_TOOLS_PROMETHEUS_URL="https://${CODESPACE_NAME}-9090.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"
  export EXTERNAL_TOOLS_MAILPIT_URL="https://${CODESPACE_NAME}-8025.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}"
fi

docker compose \
  -f docker-compose.yml \
  -f docker-compose.codespaces.yml \
  up -d --build \
  postgres redis rabbitmq mailpit api-1 api-2 worker nginx-codespaces \
  prometheus grafana postgres-exporter redis-exporter node-exporter dozzle

docker compose -f docker-compose.yml -f docker-compose.codespaces.yml ps

cat <<'EOF'

Ticketory is starting.

Open the Codespaces "Ports" tab and forward:
  8080  -> Ticketory UI
  3000  -> Grafana
  9090  -> Prometheus
  15672 -> RabbitMQ Management
  8025  -> Mailpit
  8888  -> Dozzle

Seed users: see .env (SEED_ADMIN_PASSWORD, SEED_ORGANIZER_PASSWORD, SEED_CUSTOMER_PASSWORD).

To reset the database:
  docker compose -f docker-compose.yml -f docker-compose.codespaces.yml down -v
  ./scripts/codespaces-start.sh
EOF
