#!/bin/bash
# Script para gerar massa de ordens de serviço reais, autenticando e usando dados válidos
# Uso: ./gerar-massa-ordens-v2.sh <COUNT> <URL_API>
# Exemplo: ./gerar-massa-ordens-v2.sh 100 http://localhost:5000

COUNT=${1:-10}
API_URL=${2:-http://localhost:5000}
USER_EMAIL="teste$(date +%s)@exemplo.com"
USER_NAME="usuarioteste$(date +%s)"
USER_PASSWORD="Teste@123"

# 1. Criar usuário
REG_PAYLOAD=$(cat <<EOF
{
  "nomeUsuario": "$USER_NAME",
  "email": "$USER_EMAIL",
  "senha": "$USER_PASSWORD"
}
EOF
)
REG_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_URL/api/autenticacao/registro" -H "Content-Type: application/json" -d "$REG_PAYLOAD")
echo "Status criação usuário: $REG_STATUS"

# 2. Login
LOGIN_PAYLOAD=$(cat <<EOF
{
  "nomeUsuario": "$USER_NAME",
  "senha": "$USER_PASSWORD"
}
EOF
)
TOKEN=$(curl -s -X POST "$API_URL/api/autenticacao/login" -H "Content-Type: application/json" -d "$LOGIN_PAYLOAD" | grep -o '"token"\s*:\s*"[^"]*"' | cut -d '"' -f4)
if [ -z "$TOKEN" ]; then
  echo "Falha ao obter token JWT."
  exit 1
fi

# 3. Buscar clientes, serviços e peças

# 3. Criar cliente, serviço e peça (garante dados válidos)
CLIENTE_PAYLOAD='{
  "nome": "Cliente Teste",
  "cpfCnpj": "12345678901",
  "emailPrincipal": "cliente@teste.com",
  "telefonePrincipal": "(11) 99999-9999",
  "enderecoPrincipal": "Rua Teste, 123"
}'
CLIENTE_RESPONSE=$(curl -s -X POST "$API_URL/api/clientes" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$CLIENTE_PAYLOAD")
CLIENTE_ID=$(echo "$CLIENTE_RESPONSE" | grep -o '"id"\s*:\s*[0-9]*' | head -1 | grep -o '[0-9]*')

SERVICO_PAYLOAD='{
  "nomeServico": "Troca de Óleo",
  "preco": 150.00,
  "tempoEstimadoExecucao": 60,
  "descricaoDetalhadaServico": "Troca de óleo do motor com filtro",
  "frequenciaRecomendada": "A cada 10.000 km ou 6 meses"
}'
SERVICO_RESPONSE=$(curl -s -X POST "$API_URL/api/servicos" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$SERVICO_PAYLOAD")
SERVICO_ID=$(echo "$SERVICO_RESPONSE" | grep -o '"id"\s*:\s*[0-9]*' | head -1 | grep -o '[0-9]*')

PECA_PAYLOAD='{
  "nome": "Filtro de Óleo",
  "preco": 35.00,
  "quantidadeEstoque": 100,
  "observacoes": "Filtro compatível com modelos populares"
}'
PECA_RESPONSE=$(curl -s -X POST "$API_URL/api/pecas" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$PECA_PAYLOAD")
PECA_ID=$(echo "$PECA_RESPONSE" | grep -o '"id"\s*:\s*[0-9]*' | head -1 | grep -o '[0-9]*')

if [ -z "$CLIENTE_ID" ] || [ -z "$SERVICO_ID" ] || [ -z "$PECA_ID" ]; then
  echo "Não foi possível criar ou obter IDs válidos de cliente, serviço ou peça."
  exit 1
fi

echo "Enviando $COUNT ordens de serviço para $API_URL/api/ordensservico ..."
for i in $(seq 1 $COUNT); do
  PLACA="ABC$(printf '%04d' $((RANDOM % 10000)))"
  ANO=$((2020 + (RANDOM % 5)))
  JSON=$(cat <<EOF
{
  "clienteId": $CLIENTE_ID,
  "veiculoPlaca": "$PLACA",
  "veiculoMarcaModelo": "Modelo Teste",
  "veiculoAnoFabricacao": $ANO,
  "servicosIds": [$SERVICO_ID],
  "pecas": [
    { "idPeca": $PECA_ID, "quantidade": 1 }
  ]
}
EOF
)
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "$API_URL/api/ordensservico" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" -d "$JSON"
done

echo "\nConcluído. Usuário: $USER_NAME | Email: $USER_EMAIL | Senha: $USER_PASSWORD"
