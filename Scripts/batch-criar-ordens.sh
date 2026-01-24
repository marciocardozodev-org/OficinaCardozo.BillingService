#!/bin/sh
# Script para simular o batch de criação de ordens de serviço via API
# Uso: ./batch-criar-ordens.sh <URL_BASE_API>

if [ -z "$1" ]; then
  echo "Uso: $0 <URL_BASE_API>"
  exit 1
fi

URL_BASE="$1"

for i in $(seq -w 0 19); do
  CPF="999999990${i}"
  PLACA="TESTE${i}"
  NOME="Cliente Teste ${i}"
  EMAIL="cliente${i}@teste.com"
  echo "\n--- Batch $i ---"
  echo "Buscando/criando cliente $CPF..."
  curl -s -X POST "$URL_BASE/api/Cliente/criar-ou-obter" \
    -H "Content-Type: application/json" \
    -d '{"nome":"'$NOME'","cpfCnpj":"'$CPF'","emailPrincipal":"'$EMAIL'","telefonePrincipal":"11999990000"}'
  echo "\nBuscando/criando veículo $PLACA..."
  curl -s -X POST "$URL_BASE/api/Veiculo/criar-ou-obter" \
    -H "Content-Type: application/json" \
    -d '{"placa":"'$PLACA'","marcaModelo":"Modelo Teste","anoFabricacao":2020,"cor":"Azul","tipoCombustivel":"Flex","cpfCnpjCliente":"'$CPF'"}'
  echo "\nBuscando/criando serviço de teste..."
  curl -s -X POST "$URL_BASE/api/Servico/criar-ou-obter" \
    -H "Content-Type: application/json" \
    -d '{"nomeServico":"Serviço Teste","preco":100,"tempoEstimadoExecucao":1}'
  echo "\nCriando ordem de serviço..."
  curl -s -X POST "$URL_BASE/api/OrdemServico/criar-completo" \
    -H "Content-Type: application/json" \
    -d '{"clienteCpfCnpj":"'$CPF'","veiculoPlaca":"'$PLACA'","servicosIds":[1],"pecas":[]}'
  echo "\n--- Batch $i finalizado ---"
done
