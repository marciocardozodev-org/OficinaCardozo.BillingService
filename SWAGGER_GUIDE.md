# 📚 Documentação da API - OficinaCardozo Billing Service

## 🔗 Acesso ao Swagger (OpenAPI)

### URL do Swagger UI

```
http://localhost:5000/swagger/index.html
```

ou

```
https://localhost:5001/swagger/index.html
```

### URL do OpenAPI JSON (para integração com ferramentas)

```
http://localhost:5000/swagger/v1/swagger.json
```

## 📋 Definições da API

- **Título**: OficinaCardozo Billing Service API
- **Versão**: v1
- **Descrição**: API para gestão de Ordens de Serviço, Orçamentos e Pagamentos
- **Contato**: contato@oficinacardozo.com

## ⚠️ Importante: Comportamento com Load Balancer

Devido à implementação de **Load Balancer** no ambiente de produção, o Swagger pode estar **intermitentemente indisponível**:

### 🔴 Quando o Swagger Pode Ficar Indisponível:

1. **Distribuição de Tráfego Entre Réplicas**
   - Requisições sucessivas podem ser roteadas para diferentes instâncias
   - Se uma instância reiniciar, temporariamente não conseguirá servir requisições

2. **Rebalanceamento de Instâncias**
   - Durante scale-up/scale-down do cluster Kubernetes
   - Periodicamente as réplicas podem ser recicladas

3. **Atualizações Rolling (Blue-Green Deployment)**
   - Durante deploy de novas versões
   - Instâncias antigas são desligadas gradualmente

4. **Health Check Failures**
   - Se endpoint `/health` falhar, instância é removida do balancer

### ✅ Recomendações de Uso:

```bash
# Acessar com retry automático (curl com retry)
curl --retry 3 --retry-delay 1 http://localhost:5000/swagger/index.html

# Usar em scripts com tratamento de erro
for i in {1..5}; do
  curl -s http://localhost:5000/swagger/index.html && break
  sleep 2
done

# Melhor prática: Usar ferramentas que tratam reconexão
# - Postman: Retry com backoff exponencial
# - Insomnia: Environment switching
# - Kubernetes: Probe com tolerância a falhas
```

## 🔐 Autenticação

Todas as requisições de escrita requerem **JWT Bearer Token**:

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "user", "password": "pass"}'
```

Usar o token retornado nas próximas requisições:

```bash
curl -X GET http://localhost:5000/api/billing/budgets \
  -H "Authorization: Bearer <seu_token_aqui>"
```

## 📊 Endpoints Principais

### 🎯 Orçamentos (Budgets)

```bash
# Gerar novo orçamento
POST /api/billing/budgets

# Aprovar orçamento
POST /api/billing/budgets/{osId}/approve

# Consultar orçamento
GET /api/billing/budgets/{osId}
```

### 💳 Pagamentos (Payments)

```bash
# Iniciar pagamento
POST /api/billing/payments/{osId}/start

# Webhook do Mercado Pago
POST /api/billing/mercadopago/webhook

# Consultar status de pagamento
GET /api/billing/payments/{osId}
```

### 🏥 Saúde (Health Check)

```bash
# Health check simples (sem autenticação)
GET /api/health

# Resposta esperada:
# HTTP 200 OK
```

## 🔄 CorrelationId

Todos os requests devem incluir um header `Correlation-Id` para rastreamento:

```bash
curl -X GET http://localhost:5000/api/billing/budgets/123 \
  -H "Correlation-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer <token>"
```

> **Nota**: Se não fornecido, um novo ID será gerado automaticamente.

## 📝 Exemplos de Requisições

### 1️⃣ Autenticar

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "admin123"
  }'
```

**Resposta:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### 2️⃣ Criar Orçamento

```bash
curl -X POST http://localhost:5000/api/billing/budgets \
  -H "Content-Type: application/json" \
  -H "Correlation-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer eyJhbGc..." \
  -d '{
    "osId": "550e8400-e29b-41d4-a716-446655440000",
    "valor": 1500.00,
    "emailCliente": "cliente@example.com"
  }'
```

### 3️⃣ Aprovar Orçamento

```bash
curl -X POST http://localhost:5000/api/billing/budgets/550e8400-e29b-41d4-a716-446655440000/approve \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Correlation-Id: 550e8400-e29b-41d4-a716-446655440000"
```

### 4️⃣ Iniciar Pagamento

```bash
curl -X POST http://localhost:5000/api/billing/payments/550e8400-e29b-41d4-a716-446655440000/start \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Correlation-Id: 550e8400-e29b-41d4-a716-446655440000"
```

## 🚀 Ferramentas Recomendadas para Testar

| Ferramenta | Uso | Link |
|-----------|-----|------|
| **Postman** | Testes manuais + automação | https://www.postman.com |
| **Insomnia** | Alternative ao Postman | https://insomnia.rest |
| **curl** | Linha de comando | https://curl.se |
| **HTTPie** | Alternativa moderna ao curl | https://httpie.io |
| **Swagger UI** | Documentação interativa | Embutido na API |

## 🔍 Debug e Troubleshooting

### Logs em Tempo Real

```bash
# Ver logs da aplicação
kubectl logs -f deployment/billingservice -n production

# Ver logs com filter por CorrelationId
kubectl logs -f deployment/billingservice -n production | grep "550e8400"
```

### Verificar Health Check

```bash
curl -v http://localhost:5000/api/health
```

Esperado:
```
HTTP/1.1 200 OK
```

### Verificar Disponibilidade com Retry

```bash
#!/bin/bash
URL="http://localhost:5000/swagger/index.html"
RETRIES=3
DELAY=2

for i in $(seq 1 $RETRIES); do
  echo "Tentativa $i de $RETRIES..."
  curl -s "$URL" > /dev/null
  
  if [ $? -eq 0 ]; then
    echo "✅ API disponível!"
    break
  fi
  
  if [ $i -lt $RETRIES ]; then
    echo "⏳ Aguardando $DELAY segundos..."
    sleep $DELAY
  else
    echo "❌ API indisponível após $RETRIES tentativas"
  fi
done
```

## 📊 Monitoramento e Métricas

### Prometheus Metrics

```
GET http://localhost:5000/metrics
```

Métricas disponíveis:
- `http_requests_total`
- `http_request_duration_seconds`
- `database_query_duration_seconds`
- `outbox_messages_pending`
- `sqs_messages_processed`

## 🔗 Integração com Outras Ferramentas

### Docker Compose

```bash
# Adicionar ao docker-compose.yml
curl -X GET http://billingservice:5000/api/health

# Status esperado: 200 OK
```

### Kubernetes Probe

```yaml
livenessProbe:
  httpGet:
    path: /api/health
    port: 5000
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /api/health
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 5
```

## 💡 Dicas Importantes

1. **Salve os Links**: Bookmark a URL do Swagger para acesso rápido
2. **Use Variáveis de Ambiente**: Armazene base URL e tokens em variáveis
3. **Implemente Retry Logic**: Sempre use retry em produção
4. **Monitor Health**: Acompanhe `/health` continuamente
5. **Serilog Logs**: Todos os requests são logados com CorrelationId

---

**Última Atualização**: 23 de Fevereiro de 2026

**Suporte**: contato@oficinacardozo.com
