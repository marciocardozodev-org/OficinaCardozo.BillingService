#!/bin/bash
# Script para gerar massa de ordens de serviço via endpoint real da API
# Uso: ./gerar-massa-ordens.sh <COUNT> <URL_API>
# Exemplo: ./gerar-massa-ordens.sh 100 http://localhost:5000

COUNT=${1:-100}
API_URL=${2:-http://localhost:5000}

if [ -z "$COUNT" ] || [ -z "$API_URL" ]; then
  echo "Uso: $0 <COUNT> <URL_API>"
  exit 1
fi

echo "Enviando $COUNT ordens de serviço para $API_URL/api/ordensservico ..."

for i in $(seq 1 $COUNT); do
  PLACA="ABC$(printf '%04d' $((RANDOM % 10000)))"
  CPF="1111111111$((RANDOM % 9))"
  ANO=$((2020 + (RANDOM % 5)))
  JSON=$(cat <<EOF
{
  "clienteCpfCnpj": "$CPF",
  "veiculoPlaca": "$PLACA",
  "veiculoMarcaModelo": "Modelo Teste",
  "veiculoAnoFabricacao": $ANO,
  "servicosIds": [1],
  "pecas": [
    { "idPeca": 1, "quantidade": 1 }
  ]
}
EOF
)
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "$API_URL/api/ordensservico" -H "Content-Type: application/json" -d "$JSON"
done

echo "\nConcluído."
