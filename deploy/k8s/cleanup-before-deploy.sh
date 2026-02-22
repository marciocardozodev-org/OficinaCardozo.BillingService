#!/bin/bash
# Script de limpeza pré-deploy para liberar capacidade no cluster
# Executa antes do create-db-job na pipeline

set -e

echo "🧹 Limpando recursos desnecessários no cluster..."

# 1. Deletar pods órfãos em status Failed/Error
echo "Removendo pods Failed/Error..."
kubectl delete pods --field-selector status.phase=Failed -n default --ignore-not-found=true
kubectl delete pods --field-selector status.phase=Unknown -n default --ignore-not-found=true

# 2. Deletar Jobs completados antigos (mais de 1 dia)
echo "Removendo Jobs completados antigos..."
kubectl delete jobs -n default --field-selector status.successful=1 --ignore-not-found=true || true

# 3. Verificar capacidade antes de criar novos recursos
PENDING_PODS=$(kubectl get pods --all-namespaces --field-selector status.phase=Pending --no-headers 2>/dev/null | wc -l)
if [ "$PENDING_PODS" -gt 5 ]; then
  echo "⚠️  AVISO: $PENDING_PODS pods em estado Pending no cluster"
  echo "Listando pods Pending:"
  kubectl get pods --all-namespaces --field-selector status.phase=Pending
fi

# 4. Mostrar estatísticas do cluster
echo ""
echo "📊 Estatísticas do cluster:"
echo "Total de pods: $(kubectl get pods --all-namespaces --no-headers | wc -l)"
echo "Pods Running: $(kubectl get pods --all-namespaces --field-selector status.phase=Running --no-headers | wc -l)"
echo "Pods Pending: $(kubectl get pods --all-namespaces --field-selector status.phase=Pending --no-headers | wc -l)"
echo ""

echo "✅ Limpeza concluída"
