# BillingService - Saga de Faturamento - Implementação Completa

## ✅ Status: Build Sucesso

O projeto foi implementado com sucesso, incluindo:
- **Modelos de Estado**: BudgetStatus, PaymentStatus
- **Eventos**: BudgetGenerated, BudgetApproved, PaymentConfirmed, PaymentFailed, PaymentReversed
- **EventEnvelope**: Com CorrelationId e CausationId para rastreabilidade
- **Transactional Outbox/Inbox**: Para garantir entrega confiável de eventos
- **Consumidores SQS**: Para processar OsCreated, OsCanceled, OsCompensationRequested
- **Publisher SQS**: Para publicar eventos de Billing
- **Handlers**: OsCreatedHandler, OsCanceledHandler, OsCompensationRequestedHandler
- **Endpoints REST**: GET /billing/budgets/{osId}, POST /billing/budgets/{osId}/approve, etc.
- **Integração Mercado Pago**: Mock inicial
- **Compensação**: PaymentCompensationService

## 📂 Estrutura de Arquivos Criados

### Contracts/Events/
- `BudgetStatus.cs` - Enum de estados de orçamento
- `PaymentStatus.cs` - Enum de estados de pagamento
- `EventEnvelope.cs` - Envelope genérico com CorrelationId/CausationId
- `OsCreated.cs` - Evento de entrada (do OSService)
- `OsCanceled.cs` - Evento de compensação
- `OsCompensationRequested.cs` - Evento de compensação
- `BudgetGenerated.cs` - Evento de saída
- `BudgetApproved.cs` - Evento de saída
- `BudgetRejected.cs` - Evento de saída
- `PaymentConfirmed.cs` - Evento de saída
- `PaymentFailed.cs` - Evento de saída
- `PaymentReversed.cs` - Evento de saída

### Messaging/
- `OutboxMessage.cs` - Modelo de mensagem de saída
- `InboxMessage.cs` - Modelo de mensagem de entrada (dedup)
- `IEventPublisher.cs` - Interface de publicação
- `IEventConsumer.cs` - Interface de consumo
- `SqsEventPublisher.cs` - Implementação AWS SQS
- `SqsEventConsumer.cs` - Consumidor base
- `SqsEventConsumerImpl.cs` - Implementação com handler OsCreated
- `OutboxProcessor.cs` - Processador de outbox
- `InboxProcessor.cs` - Processador de inbox

### Handlers/
- `OsCreatedHandler.cs` - Cria orçamento ao receber OsCreated
- `OsCanceledHandler.cs` - Compensa pagamento
- `OsCompensationRequestedHandler.cs` - Compensa pagamento
- `SqsEventConsumerHostedService.cs` - Background job para polling SQS

### API/Billing/
- `BillingController.cs` - Endpoints principais: /api/billing/*
- `BudgetController.cs` - GET /billing/budgets/{osId}
- `MercadoPagoService.cs` - Mock de integração Mercado Pago
- `WebhookValidator.cs` - Validação de webhook
- `PaymentCompensationService.cs` - Compensação de pagamentos
- `IdempotencyService.cs` - Dedup de eventos
- `FLUX0_TEST_ORCAMENTO.md` - Documentação de teste

### Database/
- `BillingDbContext.cs` - DbContext com Outbox/Inbox configurados
- Tabelas: orcamento, pagamento, atualizacoes_status_os, outbox_message, inbox_message

## 🔄 Fluxo Esperado: OsCreated → BudgetGenerated

```
OSService (SQS)
  └─ OsCreated
    └─ BillingService SqsEventConsumerHostedService
      └─ OsCreatedHandler.HandleAsync()
        ├─ OrcamentoService.GerarEEnviarOrcamentoAsync()
        │  └─ DB: INSERT INTO orcamento (...)
        ├─ Cria BudgetGenerated evento
        ├─ Salva OutboxMessage (não publicado)
        │  └─ DB: INSERT INTO outbox_message (...)
        └─ Publisher.PublishAsync(BudgetGenerated)
          └─ SQS: BudgetGenerated publicado

(Background) OutboxProcessor
  └─ Processa mensagens não publicadas
    └─ UPDATE outbox_message SET published = true
```

## 🧪 Como Testar

### Pré-requisitos
1. **PostgreSQL** rodando (DB já criado via K8s)
2. **LocalStack** (ou AWS SQS real) para testar messaging
3. **.NET 8.0+** instalado

### Teste 1: Verificar Banco de Dados

```bash
# Conectar ao PostgreSQL
PGPASSWORD=postgres psql -h 127.0.0.1 -U postgres -d billingservice

# Queries para validação
SELECT * FROM orcamento;
SELECT * FROM outbox_message WHERE event_type = 'BudgetGenerated';
SELECT * FROM inbox_message WHERE event_type = 'OsCreated';
```

### Teste 2: Rodar Aplicação Localmente

```bash
# No diretório raiz
dotnet run

# Swagger UI disponível em:
# http://localhost:5000/swagger/index.html
```

### Teste 3: Simular OsCreated via SQS (LocalStack)

```bash
# Iniciar LocalStack
docker run -p 4566:4566 localstack/localstack:latest

# Criar fila SQS
aws sqs create-queue \
  --queue-name os-events \
  --endpoint-url http://localhost:4566

# Enviar evento OsCreated
aws sqs send-message \
  --queue-url http://localhost:4566/000000000000/os-events \
  --message-body '{
    "correlationId": "550e8400-e29b-41d4-a716-446655440000",
    "causationId": "12345678-1234-1234-1234-123456789012",
    "timestamp": "2026-02-18T10:00:00Z",
    "payload": {
      "osId": "550e8400-e29b-41d4-a716-446655440000",
      "description": "Serviço de manutenção",
      "createdAt": "2026-02-18T10:00:00Z"
    }
  }' \
  --endpoint-url http://localhost:4566
```

### Teste 4: Validar via GET REST

```bash
# Buscar orçamento criado
curl -X GET "http://localhost:5000/billing/budgets/550e8400-e29b-41d4-a716-446655440000"

# Esperado:
# {
#   "osId": "550e8400-e29b-41d4-a716-446655440000",
#   "budget": {
#     "id": 1,
#     "ordemServicoId": 123456,
#     "valor": 100.00,
#     "emailCliente": "client@example.com",
#     "status": "Enviado",
#     "criadoEm": "2026-02-18T10:00:00Z"
#   }
# }
```

### Teste 5: Validar Outbox (Confirmação de Publicação)

```bash
# Query no DB
SELECT * FROM outbox_message 
WHERE correlation_id = '550e8400-e29b-41d4-a716-446655440000' 
ORDER BY created_at DESC 
LIMIT 1;

# Esperado:
# - event_type: 'BudgetGenerated'
# - published: true (após OutboxProcessor processar)
```

## 📋 Variáveis de Ambiente

```bash
# Database
DB_HOST=localhost
DB_NAME=billingservice
DB_USER=postgres
DB_PASSWORD=postgres

# JWT
JWT_KEY=chave-super-secreta-para-dev

# SQS
SQS_QUEUE_URL=http://localhost:4566/000000000000/billing-events
```

## 🚀 Deploy em K8s

Os arquivos de configuração já existem em `deploy/k8s/`:
- `deployment.yaml` - Deployment do BillingService
- `service.yaml` - Service Kubernetes
- `configmap.yaml` - ConfigMap com env vars
- `secret.yaml` - Secrets (tokens, chaves)
- `create-db-job.yaml` - Job para criar tabelas

```bash
# Deploy
kubectl apply -f deploy/k8s/

# Verificar
kubectl get pods -n default
kubectl logs -f deployment/billingservice -n default
```

## ⚠️ Próximas Melhorias

1. **Testes Unitários**: Implementar testes para handlers e controllers
2. **DLQ (Dead Letter Queue)**: Configurar fila de mensagens com erro
3. **Retry Policy**: Implementar retry exponencial com Polly
4. **Monitoring**: Adicionar Application Insights ou DataDog
5. **Validação**: Adicionar validações de negócio (e.g., valor máximo de orçamento)
6. **API Gateway**: Integrar com API Gateway da AWS
7. **Integration Tests**: Testes de integração com PostgreSQL e SQS reais

## 📖 Referências

- [Saga Pattern](https://microservices.io/patterns/data/saga.html)
- [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html)
- [AWS SQS Best Practices](https://docs.aws.amazon.com/AWSSimpleQueueService/)
- [Entity Framework Core - PostgreSQL](https://www.npgsql.org/efcore/)
