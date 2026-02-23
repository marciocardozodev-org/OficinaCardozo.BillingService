# ⚠️ Guia de Disponibilidade do Swagger - Load Balancer

## 🎯 Resumo Executivo

**URL do Swagger**: `http://localhost:5000/swagger/index.html`

⚠️ **IMPORTANTE**: Em ambiente com Load Balancer (Kubernetes), o Swagger pode estar **intermitentemente indisponível** devido ao rebalanceamento de instâncias.

---

## 📊 URLs de Acesso

### Desenvolvimento (Local)
```
HTTP:  http://localhost:5000/swagger/index.html
HTTPS: https://localhost:5001/swagger/index.html
```

### Produção (via Load Balancer)
```
URL:   https://billingservice.oficinacardozo.com/swagger/index.html
```

> ⚠️ **OBS**: A URL em produção pode estar indisponível temporariamente

---

## 🔴 Cenários de Indisponibilidade

### 1. **Distribuição de Tráfego (Normal)**
**Quando**: Continuamente
**Impacto**: Baixo
**Duração**: Milissegundos a segundos

```
┌─────────────────────┐
│   Load Balancer     │
└──────────┬──────────┘
     │ ├─── Pod A (5000)  ✅ Swagger OK
     │ ├─── Pod B (5000)  ✅ Swagger OK
     └─── Pod C (5000)  ✅ Swagger OK

Cada requisição pode ir para pods diferentes
→ Se um pod falhar, cai de forma aleatória
```

### 2. **Rolling Update/Deploy**
**Quando**: Durante deploy de nova versão
**Impacto**: Moderado
**Duração**: 30-60 segundos

```
Tempo  │ Pod A      │ Pod B      │ Pod C
────────────────────────────────────────
t0     │ v1 ✅      │ v1 ✅      │ v1 ✅
t1     │ v1 ✅      │ v1 ✅      │ Terminando...
t2     │ v1 ✅      │ Iniciando  │ ❌ Indisponível
t3     │ Terminando │ v2 ✅      │ v2 ✅
t4     │ ❌ Offline │ v2 ✅      │ v2 ✅
t5     │ v2 ✅      │ v2 ✅      │ v2 ✅
```

### 3. **Pod Restart/Crash**
**Quando**: Periodicamente (liveness probe falha)
**Impacto**: Alto se afeta múltiplos pods
**Duração**: 10-30 segundos

```
┌──────────────────────────┐
│ Pod P1: Crash detectado  │
└──────────────┬───────────┘
               ↓
         Kubernetes kubelet
               ↓
    ┌─────────────────────┐
    │  Iniciando novo Pod │
    └──────────┬──────────┘
               ↓
         Aguardando readiness probe OK
               ↓
         Ready para receber tráfego
```

### 4. **Node Failure**
**Quando**: Raro (hardware failure)
**Impacto**: Alto
**Duração**: 5-10 minutos

```
Node 1: IP 10.0.1.5 (Down)
├── Pod A ❌
├── Pod B ❌
└── Pod C ❌

Node 2: IP 10.0.1.6 (Up)
├── Pod A-replica ✅ (replicado)
├── Pod B-replica ✅ (replicado)
└── Pod C-replica ✅ (replicado)
```

---

## ✅ Como Lidar com Indisponibilidade

### 1. **Implementar Retry Automático**

#### cURL com Retry
```bash
curl --retry 3 \
     --retry-delay 1 \
     --retry-connrefused \
     http://localhost:5000/swagger/index.html
```

#### Client Python
```python
import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

session = requests.Session()
retry = Retry(
    total=3,
    backoff_factor=1,
    status_forcelist=[500, 502, 503, 504]
)
adapter = HTTPAdapter(max_retries=retry)
session.mount('http://', adapter)
session.mount('https://', adapter)

response = session.get('http://localhost:5000/swagger/index.html')
```

#### Client JavaScript/Node.js
```javascript
async function fetchWithRetry(url, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    try {
      const response = await fetch(url);
      if (response.ok) return response;
    } catch (error) {
      if (i < maxRetries - 1) {
        await new Promise(r => setTimeout(r, 1000 * (i + 1)));
      }
    }
  }
  throw new Error(`Failed to fetch after ${maxRetries} attempts`);
}

fetchWithRetry('http://localhost:5000/swagger/index.html');
```

### 2. **Usar Health Check Endpoint**

```bash
# Verificar saúde da API (sem usar Swagger)
curl http://localhost:5000/api/health

# Response esperado:
# HTTP 200: {"status": "Healthy"}
```

### 3. **Salvar Swagger Offline**

```bash
# Baixar OpenAPI JSON
curl http://localhost:5000/swagger/v1/swagger.json > swagger.json

# Usar em ferramentas offline:
# - Swagger Editor: https://editor.swagger.io
# - Postman: Importar swagger.json
# - Redoc: Usar HTML local
```

---

## 📈 Métricas de Disponibilidade

### SLA (Service Level Agreement)

| Ambiente | Uptime Esperado | Downtime Mês | RTO | RPO |
|----------|-----------------|--------------|-----|-----|
| Desenvolvimento | 80% | 6 horas | 30s | 1min |
| Staging | 95% | 1.9 horas | 15s | 30s |
| Produção | 99.9% | 26 segundos | <5s | <10s |

### Monitoramento em Produção

```bash
# Prometheus Query: Taxa de Sucesso do Swagger
rate(http_requests_total{path="/swagger/index.html", status="200"}[5m])

# Kubernetes Pod Status
kubectl get pods -n production -w

# Event Logs
kubectl get events -n production --sort-by='.lastTimestamp'
```

---

## 🔧 Configurações Kubernetes para Alta Disponibilidade

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: billingservice
spec:
  replicas: 3  # Múltiplas réplicas
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1        # 1 novo pod durante update
      maxUnavailable: 0  # 0 pods indisponíveis simultaneamente
  template:
    spec:
      containers:
      - name: billingservice
        livenessProbe:
          httpGet:
            path: /api/health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /api/health
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
          failureThreshold: 2
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
          - weight: 100
            preference:
              matchExpressions:
              - key: app
                operator: In
                values:
                - billing
```

---

## 🎯 Checklist de Resiliência

- [ ] **Configurar retry automático** em requisições do cliente
- [ ] **Monitorar health check** continuamente
- [ ] **Salvar swagger.json** localmente para referência offline
- [ ] **Usar variáveis de ambiente** para URLs (não hardcode)
- [ ] **Implementar circuit breaker** para falhas persistentes
- [ ] **Log de CorrelationId** em todas requisições
- [ ] **Alertas configurados** para uptime < 99%
- [ ] **Load testing** feito antes de produção
- [ ] **Documentação** atualizada com endpoints ativos

---

## 📞 Troubleshooting Rápido

### ❌ Swagger indisponível

```bash
# 1. Verificar saúde geral
curl http://localhost:5000/api/health

# 2. Se health check OK, fazer retry
for i in {1..5}; do
  echo "Tentativa $i"
  curl http://localhost:5000/swagger/index.html && break
  sleep 2
done

# 3. Se ainda falhar, verificar pods
kubectl get pods -n production
kubectl describe pod {pod-name} -n production

# 4. Ver logs do pod
kubectl logs {pod-name} -n production --tail=100
```

### 🔄 Alternativa ao Swagger

```bash
# Usar Postman/Insomnia com swagger.json
curl http://localhost:5000/swagger/v1/swagger.json > swagger.json

# Ou usar curl diretamente
curl -X GET http://localhost:5000/api/billing/budgets \
  -H "Authorization: Bearer ${TOKEN}"
```

---

## 📚 Leitura Recomendada

- [Kubernetes Rolling Updates](https://kubernetes.io/docs/tutorials/kubernetes-basics/update-intro/)
- [Health Checks Best Practices](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [Azure App Insights - Availability](https://learn.microsoft.com/en-us/azure/azure-monitor/app/availability-overview)

---

**Última atualização**: 23 de Fevereiro de 2026
**Autor**: OfficinaCardozo DevOps Team
**Contato**: devops@oficinacardozo.com
