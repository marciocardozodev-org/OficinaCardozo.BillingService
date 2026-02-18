# Fluxo de Teste - BillingService Saga

## 1. Verificar Orçamento Gerado (OsCreated)

### Objetivo
Validar que BillingService consome `OsCreated` e cria orçamento no DB local.

### Fluxo de Teste

**Passo 1: Simular Evento OsCreated**

O evento `OsCreated` é enviado via SQS pelo OSService. Exemplo de payload:

```json
{
  "correlationId": "d3f4c5a6-1b2c-4d5e-6f7g-8h9i0j1k2l3m",
  "causationId": "a1b2c3d4-5e6f-7g8h-9i0j-1k2l3m4n5o6p",
  "timestamp": "2026-02-18T10:00:00Z",
  "payload": {
    "osId": "550e8400-e29b-41d4-a716-446655440000",
    "description": "Serviço de manutenção",
    "createdAt": "2026-02-18T10:00:00Z"
  }
}
```

**Passo 2: BillingService Processa OsCreated**

O `OsCreatedHandler` será acionado:
1. Consome evento SQS
2. Cria orçamento no DB local (tabela `orcamento`)
3. Publica `BudgetGenerated` via Outbox para SQS (confirmação ao OSService)

**Passo 3: Validar via GET REST**

```bash
GET /billing/budgets/{osId}

# Exemplo:
curl -X GET "http://localhost:5000/billing/budgets/550e8400-e29b-41d4-a716-446655440000"
```

**Resposta esperada (200 OK):**

```json
{
  "osId": "550e8400-e29b-41d4-a716-446655440000",
  "budget": {
    "id": 1,
    "ordemServicoId": 123456,
    "valor": 100.00,
    "emailCliente": "client@example.com",
    "status": "Enviado",
    "criadoEm": "2026-02-18T10:00:00Z"
  }
}
```

**Resposta esperada (404 Not Found):**
Se o orçamento não foi criado, retorna:

```json
{
  "message": "Orçamento não encontrado para o osId informado"
}
```

## 2. Verificação de Outbox/Inbox

### Validar Outbox (BudgetGenerated publicado)

Query no DB:

```sql
SELECT * FROM outbox_message 
WHERE event_type = 'BudgetGenerated' 
ORDER BY created_at DESC 
LIMIT 1;
```

Esperado: Uma linha com:
- `event_type`: `BudgetGenerated`
- `published`: `true` (após OutboxProcessor processar)
- `correlation_id`: mesmo ID do evento OsCreated

### Validar Inbox (OsCreated recebido)

Query no DB:

```sql
SELECT * FROM inbox_message 
WHERE event_type = 'OsCreated' 
ORDER BY received_at DESC 
LIMIT 1;
```

Esperado: Uma linha com:
- `event_type`: `OsCreated`
- `provider_event_id`: ID único do evento (para dedup)

## 3. Fluxo Completo Esperado

```
OSService (OsCreated via SQS)
    ↓
BillingService SqsEventConsumer
    ↓
OsCreatedHandler
    ↓ Cria Orcamento
DB: orcamento (nova linha)
    ↓
OutboxMessage (nova linha, não publicado)
    ↓
OutboxProcessor (background job)
    ↓ Publica via SQS
BudgetGenerated (SQS)
    ↓
OSService consome BudgetGenerated
```

## 4. Resiliência

- **Idempotência**: `InboxMessage` dedup via `provider_event_id`
- **Outbox Pattern**: `OutboxMessage` garante publicação confiável mesmo se SQS falhar
- **Retries**: OutboxProcessor tentará publicar novamente se falhar
- **DLQ**: Mensagens com erro vão para Dead Letter Queue

## 5. Testes Locais

### Mock SQS (LocalStack)

```bash
docker run -p 4566:4566 localstack/localstack:latest
aws sqs create-queue --queue-name os-events --endpoint-url http://localhost:4566
aws sqs send-message --queue-url http://localhost:4566/000000000000/os-events \
  --message-body '{"correlationId":"...","payload":{"osId":"550e8400..."}}'
```

### Teste com curl

```bash
# Criar orçamento via REST (sem SQS, mock)
POST /api/billing/orcamento
Content-Type: application/json

{
  "ordemServicoId": 123,
  "valor": 100.00,
  "emailCliente": "test@example.com"
}

# Buscar orçamento criado
GET /billing/budgets/550e8400-e29b-41d4-a716-446655440000
```

## 6. Validação em Desenvolvimento

1. **Conexão ao DB PostgreSQL**:
   ```bash
   PGPASSWORD=postgres psql -h localhost -U postgres -d billingservice -c "SELECT * FROM orcamento LIMIT 1;"
   ```

2. **Verificar logs da aplicação**:
   ```bash
   dotnet run
   ```

3. **Swagger UI**:
   ```
   http://localhost:5000/swagger/index.html
   ```
