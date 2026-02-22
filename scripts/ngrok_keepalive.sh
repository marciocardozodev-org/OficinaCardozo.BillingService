#!/usr/bin/env bash
set -euo pipefail

PORT="${1:-8080}"
NAMESPACE="${NAMESPACE:-default}"
SERVICE_NAME="${SERVICE_NAME:-billingservice}"
LOG_FILE="${LOG_FILE:-/tmp/ngrok.log}"
PF_LOG_FILE="${PF_LOG_FILE:-/tmp/port_forward.log}"
CHECK_URL="http://localhost:4040/api/tunnels"

restart_ngrok() {
  pkill -f "ngrok http" 2>/dev/null || true
  nohup ngrok http "$PORT" > "$LOG_FILE" 2>&1 &
}

restart_port_forward() {
  pkill -f "kubectl port-forward" 2>/dev/null || true
  nohup kubectl port-forward -n "$NAMESPACE" "svc/$SERVICE_NAME" "$PORT":80 > "$PF_LOG_FILE" 2>&1 &
}

ensure_port_forward() {
  if ! curl -s "http://localhost:${PORT}/health" >/dev/null 2>&1; then
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] port-forward offline, reiniciando..."
    restart_port_forward
    sleep 5
  fi
}

get_public_url() {
  curl -s "$CHECK_URL" | python3 -c "import sys, json; data=json.load(sys.stdin); t=data['tunnels'][0] if data.get('tunnels') else None; print(t['public_url'] if t else '')"
}

while true; do
  ensure_port_forward
  url="$(get_public_url)"
  if [[ -z "$url" ]]; then
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] ngrok offline, reiniciando..."
    restart_ngrok
    sleep 5
    url="$(get_public_url)"
  fi

  if [[ -n "$url" ]]; then
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] ngrok online: ${url}/api/billing/mercadopago/webhook"
  fi

  sleep 30
 done
