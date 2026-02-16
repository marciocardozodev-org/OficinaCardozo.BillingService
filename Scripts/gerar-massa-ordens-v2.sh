#!/bin/bash
# Script para gerar massa de ordens de serviço reais, autenticando e usando dados válidos
# Uso: ./gerar-massa-ordens-v2.sh <COUNT> <URL_API>
# Exemplo: ./gerar-massa-ordens-v2.sh 100 http://localhost:5000

COUNT=20
API_URL=${1:-http://localhost:5000}

USER_NAME="admin"
USER_PASSWORD="Password123!"





LOGIN_RESPONSE=$(curl -X POST "$API_URL/api/autenticacao/login" -H "Content-Type: application/json" -d '{"nomeUsuario":"admin","senha":"Password123!"}' -i)
echo "Resposta login: $LOGIN_RESPONSE"
TOKEN=$(echo "$LOGIN_RESPONSE" | grep -o '"token"\s*:\s*"[^"]*"' | cut -d '"' -f4)
if [ -z "$TOKEN" ]; then
  echo "Falha ao obter token JWT. Corpo da resposta do login:" >&2
  echo "$LOGIN_RESPONSE" >&2
  exit 1
fi

# 3. Buscar clientes, serviços e peças

# 3. Criar cliente, serviço e peça (garante dados válidos)
CLIENTE_PAYLOAD='{
  "nome": "Cliente Teste",
  "cpfCnpj": "35496518806",
  "emailPrincipal": "cliente@teste.com",
  "telefonePrincipal": "(11) 99999-9999",
  "enderecoPrincipal": "Rua Teste, 123"
}'

# Tenta criar o cliente
CLIENTE_RESPONSE=$(curl -s -X POST "$API_URL/api/clientes" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$CLIENTE_PAYLOAD")
echo "Resposta /api/clientes: $CLIENTE_RESPONSE"
CLIENTE_ID=$(echo "$CLIENTE_RESPONSE" | grep -o '"id"\s*:\s*[0-9]*' | head -1 | grep -o '[0-9]*')

# Se não conseguiu criar, busca o cliente pelo CPF
if [ -z "$CLIENTE_ID" ]; then
  BUSCA_CLIENTE=$(curl -s -X GET "$API_URL/api/clientes?cpfCnpj=35496518806" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN")
  echo "Busca cliente existente: $BUSCA_CLIENTE"
  CLIENTE_ID=$(echo "$BUSCA_CLIENTE" | grep -o '"id"\s*:\s*[0-9]*' | head -1 | grep -o '[0-9]*')
fi

SERVICO_NOME="Troca de Óleo $(date +%s%N | cut -b1-13)"
SERVICO_PAYLOAD='{
  "nomeServico": "'$SERVICO_NOME'",
  "preco": 150.00,
  "tempoEstimadoExecucao": 60,
  "descricaoDetalhadaServico": "Troca de óleo do motor com filtro",
  "frequenciaRecomendada": "A cada 10.000 km ou 6 meses"
}'
SERVICO_RESPONSE=$(curl -s -X POST "$API_URL/api/servicos" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$SERVICO_PAYLOAD")
echo "Resposta /api/servicos: $SERVICO_RESPONSE"
SERVICO_ID=$(echo "$SERVICO_RESPONSE" | grep -o '"id"\s*:\s*[0-9]*' | head -1 | grep -o '[0-9]*')


# Cria uma nova peça com estoque suficiente para garantir ordens
PECA_NOME="Pastilha de Freio $(date +%s%N | cut -b1-13)"
PECA_CODIGO="PASTILHA-$(date +%s%N | cut -b1-13)"
PECA_PAYLOAD='{
  "nomePeca": "'$PECA_NOME'",
  "codigoIdentificador": "'$PECA_CODIGO'",
  "preco": 50.00,
  "quantidadeEstoque": 100,
  "quantidadeMinima": 5,
  "unidadeMedida": "un",
  "localizacaoEstoque": "Prateleira 2",
  "observacoes": "Pastilha para teste de massa"
}'
PECA_RESPONSE=$(curl -s -X POST "$API_URL/api/pecas" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$PECA_PAYLOAD")
echo "Resposta /api/pecas: $PECA_RESPONSE"
PECA_ID=$(echo "$PECA_RESPONSE" | grep -o '"id"\s*:\s*[0-9]*' | head -1 | grep -o '[0-9]*')

if [ -z "$CLIENTE_ID" ] || [ -z "$SERVICO_ID" ] || [ -z "$PECA_ID" ]; then
  echo "Não foi possível criar ou obter IDs válidos de cliente, serviço ou peça."
  exit 1
fi

echo "Enviando $COUNT ordens de serviço para $API_URL/api/ordensservico ..."

for i in $(seq 1 $COUNT); do
  PLACA="SWG-$(printf '%04d' $((1000 + i)))"
  ANO=$((2020 + (i % 5)))
  JSON=$(cat <<EOF
{
  "clienteCpfCnpj": "35496518806",
  "veiculoPlaca": "$PLACA",
  "veiculoMarcaModelo": "Fiat Uno $i",
  "veiculoAnoFabricacao": $ANO,
  "veiculoCor": "Prata",
  "servicosIds": [$SERVICO_ID],
  "pecas": [
    { "idPeca": $PECA_ID, "quantidade": 1 }
  ]
}
EOF
)
  RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/api/ordensservico" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" -d "$JSON")
  BODY=$(echo "$RESPONSE" | head -n -1)
  STATUS=$(echo "$RESPONSE" | tail -n1)
  echo "Ordem $i: [HTTP $STATUS] $BODY"
done

echo "\nConcluído. Usuário: $USER_NAME | Email: $USER_EMAIL | Senha: $USER_PASSWORD"
