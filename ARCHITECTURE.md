# 🏗️ Arquitetura do Microserviço de Billing - OficinaCardozo

**Versão**: 1.0  
**Data**: Fevereiro 2026  
**Autor**: Equipe OficinaCardozo  

---

## 📋 Índice

1. [Visão Geral Arquitetural](#visão-geral-arquitetural)
2. [Estrutura de Camadas](#estrutura-de-camadas)
3. [Fluxos de Negócio Principais](#fluxos-de-negócio-principais)
4. [Padrões de Design](#padrões-de-design)
5. [Integrações Externas](#integrações-externas)
6. [Banco de Dados](#banco-de-dados)
7. [Segurança](#segurança)
8. [Observabilidade](#observabilidade)
9. [Deployment](#deployment)
10. [Decisões Arquiteturais](#decisões-arquiteturais)

---

## 🎯 Visão Geral Arquitetural

### Responsabilidades Principais

O **BillingService** é um microserviço responsável por:

- ✅ **Gestão de Orçamentos**: Geração, aprovação e consulta de orçamentos
- ✅ **Processamento de Pagamentos**: Integração com MercadoPago
- ✅ **Atualização de Status**: Rastreamento e auditoria de mudanças de status
- ✅ **Comunicação Assíncrona**: Consumo e publicação de eventos via AWS SQS/SNS
- ✅ **Garantia de Entrega**: Padrão Transactional Outbox/Inbox para consistência

### Diagrama de Arquitetura

```
┌─────────────────────────────────────────────────────────────────┐
│                      API Gateway / Load Balancer                │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTP/HTTPS (Porta 5000/5001)
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│              OficinaCardozo.BillingService                       │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ API Layer (REST Controllers, Middleware)                 │   │
│  │ - BillingController (Orçamentos, Pagamentos)             │   │
│  │ - CorrelationIdMiddleware (Request Tracing)              │   │
│  │ - ExceptionHandlingMiddleware (Error Handling)           │   │
│  │ - ValidationFilter (Model Validation)                    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                           ▲                                       │
│  ┌────────────────────────┴┬───────────────────────────────┐    │
│  │                         │                               │    │
│  ▼                         ▼                               ▼    │
│ ┌──────────────────┬──────────────────┬──────────────────┐      │
│ │ Application Layer                                       │      │
│ │                                                         │      │
│ │ • OrcamentoService                                      │      │
│ │ • PagamentoService                                      │      │
│ │ • AtualizacaoStatusOsService                            │      │
│ │ • ServiceOrchestrator                                   │      │
│ └─────────────────┬────────────────────────────────────────┘     │
│                   │                                               │
│  ┌────────────────┴─────────────────┐                            │
│  │                                  │                            │
│  ▼                                  ▼                            │
│ ┌──────────────────┐       ┌───────────────────┐                │
│ │ Domain Layer     │       │ Event Handlers    │                │
│ │ (Entities)       │       │                   │                │
│ │                  │       │ • OsCreatedHandler│                │
│ │ • Orcamento      │       │ • OsCanceledHandler
│ │ • Pagamento      │       │ • OsCompensation  │                │
│ │ • StatusUpdate   │       │   RequestedHandler│                │
│ └──────────────────┘       └───────────────────┘                │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ Infrastructure Layer                                       │  │
│  │                                                            │  │
│  │ ┌──────────────────┬──────────────┬──────────────────┐    │  │
│  │ │ Persistence      │ Messaging    │ External APIs    │    │  │
│  │ │                  │              │                  │    │  │
│  │ │ • BillingDbCtx   │ • Outbox     │ • MercadoPago    │    │  │
│  │ │  (PostgreSQL)    │ • Inbox      │                  │    │  │
│  │ │                  │ • SQS        │                  │    │  │
│  │ └──────────────────┴──────────────┴──────────────────┘    │  │
│  └────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
         │                         │                    │
         ▼                         ▼                    ▼
    PostgreSQL           AWS SQS/SNS            MercadoPago API
     Database            (Event Stream)          Payment Gateway
```

---

## 🏢 Estrutura de Camadas

### 1️⃣ **API Layer** (`/src/API`)

Responsável por:
- Exposição de endpoints REST
- Validação de requisições HTTP
- Tratamento centralizado de exceções
- Rastreamento de correlação (CorrelationId)

#### Controllers

| Controller | Endpoints |
|-----------|-----------|
| `BillingController` | `POST /api/billing/budgets` - Criar orçamento<br>`GET /api/billing/budgets/{osId}` - Obter orçamento<br>`POST /api/billing/budgets/{osId}/approve` - Aprovar orçamento<br>`POST /api/billing/payments` - Iniciar pagamento<br>`GET /api/billing/payments/{osId}` - Obter status de pagamento |

#### Middleware

| Middleware | Função |
|-----------|--------|
| `CorrelationIdMiddleware` | Extrai/gera CorrelationId único por requisição<br>Inclui ID em tous os logs e traces distribuídos |
| `ExceptionHandlingMiddleware` | Captura exceções não tratadas<br>Retorna respostas padronizadas com status HTTP apropriado |
| `AuthenticationMiddleware` | Valida JWT Bearer tokens para operações sensíveis |

#### Filters

| Filter | Função |
|--------|--------|
| `ValidationFilter` | Valida ModelState antes de chegar ao controller<br>Retorna 400 Bad Request com detalhes de erro |

---

### 2️⃣ **Application Layer** (`/src/Application`)

Implementa a lógica de negócio orquestrada pelos serviços.

#### OrcamentoService
```csharp
Responsabilidades:
├── GerarEEnviarOrcamentoAsync(osId, valor, email)
│   └── Cria novo orçamento
│   └── Publica evento BudgetGenerated no Outbox
│
├── AprovaBudgetAsync(osId)
│   └── Marca orçamento como aprovado
│   └── Publica evento BudgetApproved
│
└── GetBudgetByOsIdAsync(osId)
    └── Consulta orçamento existente
```

#### PagamentoService
```csharp
Responsabilidades:
├── IniciarPagamentoAsync(orcamentoId, email, cardToken)
│   └── Interage com MercadoPago API
│   └── Armazena resultado em DB
│   └── Publica evento PaymentInitiated
│
├── AtualizarStatusPagamentoAsync(osId, status)
│   └── Atualiza status do pagamento
│   └── Publica evento PaymentStatusUpdated
│
└── GetPagamentoByOsIdAsync(osId)
    └── Consulta dados de pagamento
```

#### AtualizacaoStatusOsService
```csharp
Responsabilidades:
├── AtualizarStatusAsync(osId, novoStatus, motivo)
│   └── Registra mudança de status
│   └── Armazena histórico para auditoria
│   └── Publica evento OsStatusChanged
│
└── ObterHistoricoAsync(osId)
    └── Retorna timeline de mudanças
```

#### ServiceOrchestrator
```csharp
Responsabilidades:
└── OrquestraFluxoCompletoAsync(osId)
    ├── Coleta dados de serviços
    ├── Valida consistência
    └── Coordena operações multi-serviço
```

---

### 3️⃣ **Domain Layer** (`/src/Domain`)

Define as entidades de negócio e regras invariantes.

#### Orcamento
```csharp
Propriedades:
├── Id (Guid) - PK
├── OsId (Guid) - FK para Ordem de Serviço
├── Valor (decimal) - Valor do serviço
├── EmailCliente (string) - Email para envio
├── Status (StatusOrcamento) - Estado atual
│   └── Enum: Enviado, Aprovado, Recusado, Expirado
├── CorrelationId (Guid) - Rastreamento
├── CausationId (Guid) - Rastreamento causal
├── CriadoEm (DateTime UTC)
└── AtualizadoEm (DateTime UTC)

Indexes:
├── (OsId) - Consultas por OS
└── (CorrelationId) - Rastreamento distribuído
```

#### Pagamento
```csharp
Propriedades:
├── Id (Guid) - PK
├── OsId (Guid) - FK para Ordem de Serviço
├── OrcamentoId (Guid) - FK para Orçamento
├── Valor (decimal) - Valor cobrado
├── Metodo (string) - "MercadoPago", "Boleto", etc.
├── Status (StatusPagamento)
│   └── Enum: Pendente, Processando, Aprovado, Rejeitado, Cancelado
├── MercadoPagoId (string) - ID da transação
├── Descricao (string) - Detalhes da resposta
├── CorrelationId (Guid)
├── CriadoEm (DateTime UTC)
└── AtualizadoEm (DateTime UTC)

Indexes:
├── (OsId, MercadoPagoId)
└── (CorrelationId)
```

#### AtualizacaoStatusOs
```csharp
Propriedades:
├── Id (Guid) - PK
├── OsId (Guid) - FK
├── StatusAnterior (string) - State before change
├── NovoStatus (string) - State after change
├── Motivo (string) - Reason for change
├── AlteradoPor (string) - User/Service identifier
├── AtualizadoEm (DateTime UTC)
└── CorrelationId (Guid)

Index:
└── (OsId, AtualizadoEm DESC) - Histórico ordenado
```

---

### 4️⃣ **Event Handlers** (`/src/Handlers`)

Componentes assíncronos que reagem a eventos do sistema.

#### OsCreatedHandler
```
Trigger: Evento OsCreated recebido via SQS
│
├─ Verificar idempotência (InboxMessage)
├─ Extrair dados do evento
│  └─ Se não houver valor, usar default 100.00
├─ Criar Orcamento via OrcamentoService
├─ Persistir OutboxMessage (BudgetGenerated)
└─ Marcar InboxMessage como processada

Garantia: Exactly-once processing
```

#### OsCanceledHandler
```
Trigger: Evento OsCanceled recebido via SQS
│
├─ Buscar orçamento associado
├─ Cancelar pagamentos em aberto
├─ Publicar evento CompensationRequested
└─ Audit log da mudança

Garantia: Transações ACID
```

#### OsCompensationRequestedHandler
```
Trigger: Evento CompensationRequested
│
├─ Revisar pagamentos já feitos
├─ Solicitar reembolso via MercadoPago
├─ Atualizar status para "Reembolsado"
└─ Notificar cliente via email
```

#### SqsEventConsumerHostedService
```
Background Service que:
├─ Inicia automaticamente com a aplicação
├─ Pooling contínuo de mensagens SQS
├─ Deserializa e roteia eventos
├─ Trata falhas com retry exponencial
└─ Logs detalhados com CorrelationId
```

---

### 5️⃣ **Messaging Layer** (`/src/Messaging`)

Implementa padrões de mensageria confiável.

#### Padrão Transactional Outbox

**Objetivo**: Garantir que eventos são publicados junto com mudanças de estado

```
Fluxo:
┌──────────────────────────────────────────────────┐
│ 1. Criar Orcamento                               │
│    BEGIN TRANSACTION                             │
│    ├─ INSERT INTO orcamento (...)                │
│    ├─ INSERT INTO outbox_message (...)           │
│    └─ COMMIT                                     │
└──────────────────────────────────────────────────┘
                      │
                      ▼
┌──────────────────────────────────────────────────┐
│ 2. Background Job (OutboxPublisher)              │
│    ├─ SELECT * FROM outbox_message               │
│    │  WHERE published = false                    │
│    ├─ Para cada mensagem:                        │
│    │  ├─ Publicar em SNS/SQS                     │
│    │  └─ UPDATE outbox_message SET published=true
│    └─ Retry automático em caso de falha          │
└──────────────────────────────────────────────────┘
                      │
                      ▼
┌──────────────────────────────────────────────────┐
│ 3. Event Dispatcher (Handler)                    │
│    ├─ Receber mensagem de SQS                    │
│    ├─ Log em InboxMessage (idempotência)        │
│    ├─ Executar handler específico               │
│    └─ Marcar InboxMessage como processed        │
└──────────────────────────────────────────────────┘
```

#### OutboxMessage
```csharp
┌─────────────────────────────────────────┐
│ Tabela: outbox_message                  │
├─────────────────────────────────────────┤
│ id (PK)           : UUID                │
│ aggregate_id      : UUID (OsId)         │
│ aggregate_type    : string ("OS")       │
│ event_type        : string ("OsCreated")│
│ payload           : JSON (evento)       │
│ created_at        : timestamp UTC       │
│ published         : boolean (default: false)
│ published_at      : timestamp UTC (nullable)
│ correlation_id    : UUID (rastreamento)│
│ causation_id      : UUID (causalidade) │
└─────────────────────────────────────────┘
```

#### InboxMessage
```csharp
┌─────────────────────────────────────────┐
│ Tabela: inbox_message                   │
├─────────────────────────────────────────┤
│ id (PK)           : UUID                │
│ event_type        : string              │
│ payload           : JSON                │
│ provider_event_id : string (SQS id)     │
│ received_at       : timestamp UTC       │
│ correlation_id    : UUID                │
│ causation_id      : UUID                │
│ processed         : boolean (default: false)
│ processed_at      : timestamp UTC (nullable)
└─────────────────────────────────────────┘

Índice UNIQUE: (provider_event_id)
Propósito: Evitar duplicação (exactly-once)
```

---

## 🔄 Fluxos de Negócio Principais

### Fluxo 1: Geração de Orçamento (Event-Driven)

```
1️⃣ Sistema de Order Service publica evento "OsCreated"
   └─ Contém: OsId, valor do serviço, email do cliente
   
2️⃣ BillingService recebe via SQS (OsCreatedHandler)
   ├─ Verifica: Já foi processado? (Inbox idempotência)
   ├─ Extrai dados: osId, valor (com fallback)
   ├─ Cria Orcamento no DB
   ├─ Insere OutboxMessage (BudgetGenerated)
   └─ Commit atômico
   
3️⃣ OutboxPublisher (background job)
   ├─ Busca OutboxMessages não publicadas
   ├─ Envia para SNS/SQS
   ├─ Marca como published
   └─ Retry exponencial em caso de falha

4️⃣ Notificação retorna para sistema principal
   └─ Sistema marca OS como "Orçamento Enviado"

Sequência Temporal:
├─ T+0ms: OsCreated consumido
├─ T+10ms: Orcamento criado + Outbox
├─ T+25ms: OutboxPublisher detecta mensagem
├─ T+50ms: BudgetGenerated entregue em SNS
├─ T+100ms: Sistema notificado
└─ T+150ms: OS atualizada no sistema principal

Garantias:
✅ Transactional Outbox: Nenhum evento é perdido
✅ Exactly-once: InboxMessage evita duplicação
✅ Eventual consistency: Dados propagados assincronamente
```

### Fluxo 2: Processamento de Pagamento (Síncrono + Assincrono)

```
Cliente HTTP Request
    │
    ▼
POST /api/billing/payments
├─ Body: { orcamentoId, cardToken, ... }
├─ Autenticação: JWT Bearer
│
▼ PagamentoService.IniciarPagamentoAsync()
├─ 1. Buscar Orcamento (validação)
├─ 2. Chamar MercadoPago API (Síncrono)
│  │   └─ Token do cartão → ID de transação
│  │
│  ├─ 2a. Resposta OK (201 Created)
│  │  ├─ Status: APPROVED
│  │  ├─ MercadoPagoId: "12345678"
│  │  └─ Salvar Pagamento em DB
│  │
│  ├─ 2b. Resposta com erro
│  │  ├─ Status: REJECTED
│  │  ├─ Descricao: motivo da rejeição
│  │  └─ Salvar tentativa falhada
│  │
│  └─ 2c. Timeout/NetworkError
│     ├─ Status: PENDING
│     ├─ Marcar para retry manual
│     └─ PublicarPaymentInitiated (será processado depois)
│
├─ 3. Salvar OutboxMessage (PaymentInitiated/PaymentUpdated)
├─ 4. HTTP 200 OK (resposta síncrona)
│
▼ Background: OutboxPublisher
├─ Publica PaymentInitiated para SNS
│
▼ Order Service consome evento
├─ Atualiza status de pagamento da OS
└─ Notifica cliente

Fluxo de Erro - Retry para MercadoPago:
┌─────────────────────────────────────┐
│ Se status = PENDING (nunca obteve resposta)
│
├─ T+5min: Retry automático
├─ T+10min: Retry automático
├─ T+30min: Manual review necessário
│          (alertar operacional)
└─ T+60min: Cancelar transação
```

### Fluxo 3: Cancelamento e Compensação

```
ORDER SERVICE: OS cancelada
    │
    ▼
Publica: OsCanceled { osId, motivo }
    │
    ▼
BILLING SERVICE: OsCanceledHandler
    │
    ├──1. Buscar orçamento
    ├──2. Buscar pagamento
    │
    ├──IF pagamento.status = "APPROVED":
    │  │
    │  ├──3a. Chamar MercadoPago refund API
    │  │  └─ Retorna refund_id
    │  │
    │  ├──3b. Atualizar Pagamento
    │  │  └─ status = "REFUNDED"
    │  │
    │  └──3c. Publicar OutboxMessage
    │     └─ CompensationApplied
    │
    └──ELSE:
       └─ Apenas marcar como cancelado

Garantia: Idempotência
└─ Se reprocessado: Verificar se refund já existe
    └─ Não duplicar reembolso

Timeline:
├─ T+0ms: OsCanceled recebido
├─ T+50ms: Refund API chamada
├─ T+500ms: Refund processado
├─ T+600ms: OutboxMessage criada
├─ T+650ms: CompensationApplied publicado
└─ T+700ms: Cliente notificado
```

---

## 🎨 Padrões de Design

### 1. **Transactional Outbox Pattern**

**Problema**: Como garantir atomicidade entre atualizar DB e publicar evento?

**Solução**:
```
Sem Outbox (❌ perigoso):
┌──────────────────┐
│ 1. INSERT orcamento
└──────────────────┘
         ↓ Se falhar aqui, evento é perdido
┌──────────────────┐
│ 2. PUBLISH evento
└──────────────────┘

Com Outbox (✅ seguro):
┌──────────────────────────────────────┐
│ 1. BEGIN TRANSACTION                 │
│ 2. INSERT orcamento                  │
│ 3. INSERT outbox_message (dto JSON)  │
│ 4. COMMIT (Atomicidade!)             │
└──────────────────────────────────────┘
         ↓
┌──────────────────────────────────────┐
│ Background Job (lê periodicamente)    │
│ - Seleciona outbox não publicados     │
│ - Publica em SNS/SQS                 │
│ - Marca como published               │
│ - Retry com exponential backoff       │
└──────────────────────────────────────┘
```

**Benefício**: Zero eventos perdidos

---

### 2. **Transactional Inbox Pattern**

**Problema**: Como evitar processar o mesmo evento duas vezes?

**Solução**:
```
Fluxo de Handler:

1. Receber mensagem de SQS
   └─ Contém ProviderEventId (ID único do SQS)

2. Consultar InboxMessage
   ├─ SELECT * WHERE provider_event_id = id
   │
   ├─ Se EXISTS:
   │  └─ if processed: RETURN (idempotente ✅)
   │
   └─ Se NOT EXISTS:
      └─ Prosseguir para step 3

3. Iniciar transação
   ├─ INSERT InboxMessage (processed=false)
   ├─ Executar lógica do handler
   ├─ UPDATE InboxMessage SET processed=true
   └─ COMMIT

4. Se falhar:
   ├─ Rollback automático
   ├─ InboxMessage ainda não marcada
   └─ SQS fará retry automático

Resultado: Exactly-once delivery guarantee ✅
```

---

### 3. **Saga Pattern para Transações Distribuídas**

**Cenário**: OsCreatedHandler precisa coordenar múltiplos passos

```
Saga: ProcessarNovaOs
│
├─ Passo 1: Criar Orcamento
│  └─ Compensation: Remover orcamento
│
├─ Passo 2: Validar com serviço X
│  └─ Compensation: Avisar serviço X da validação falha
│
└─ Passo 3: Marcar como Processado
   └─ Compensation: Marcar como não-processado

Se tudo OK: Saga conclui normalmente

Se falha em Passo 2:
├─ Desfazer Passo 2
├─ Desfazer Passo 1 (compensation)
└─ Publica evento "OsProcessingFailed"
```

---

### 4. **Event Versioning**

**Problema**: Como evoluir eventos sem quebrar consumers antigos?

**Solução**:
```csharp
// Versão 1
public class OsCreated
{
    public Guid OsId { get; set; }
    public string ClientEmail { get; set; }
}

// Versão 2 (compatível com V1)
public class OsCreatedV2
{
    public Guid OsId { get; set; }
    public string ClientEmail { get; set; }
    public decimal? Valor { get; set; }  // ← NOVO (nullable)
    public DateTime CreatedAt { get; set; } // ← NOVO
}

// Estratégia:
// 1. Code publishes BOTH V1 e V2
// 2. Old handlers processam V1
// 3. New handlers processam V2
// 4. Após todos consumers atualizarem: remover V1

// No handler:
public async Task HandleAsync(EventEnvelope<OsCreated> envelope)
{
    var valor = envelope.Payload.Valor ?? DefaultValue; // V2 compatibility
}
```

---

### 5. **Circuit Breaker Pattern**

**Aplicação**: Chamadas para MercadoPago API

```
Estados:

CLOSED ──(threshold exceed)──→ OPEN
 ✅      de falhas              ❌
 │                              │
 └──────(timeout)─────(success)──┘
                        ↓
                    HALF_OPEN
                      (testando)

Implementação (Polly):
builder.Services
    .AddHttpClient<MercadoPagoApi>()
    .AddTransientHttpErrorPolicy()
    .CircuitBreakerAsync(failureThreshold: 5, timeout: 30s);

Benefício: Falha rápida, não sobrecarrega MercadoPago
```

---

## 🔌 Integrações Externas

### AWS SQS/SNS (Event Streaming)

```
Tópicos SNS:
├── billing-events
│   └── Publicado por: OrcamentoService, PagamentoService
│       Consumido por: OrderService, NotificationService
│
└── compensation-events
    └── Publicado por: OsCompensationRequestedHandler
        Consumido por: RefundService

Filas SQS:
├── billing-service-queue
│   └── Recebe eventos: OsCreated, OsCanceled
│       Consumidor: OsCreatedHandler, OsCanceledHandler
│
└── billing-dlq (Dead Letter Queue)
    └── Mensagens que falharam 3 vezes
        Monitora: CloudWatch Alarm
```

**Configuração**:
```csharp
var sqsClient = new AmazonSQSClient(
    new BasicAWSCredentials(accessKey, secretKey),
    RegionEndpoint.SaEast1
);

var snsClient = new AmazonSimpleNotificationServiceClient(
    new BasicAWSCredentials(accessKey, secretKey),
    RegionEndpoint.SaEast1
);

// Subscribe SQS queue to SNS topic
await snsClient.SubscribeAsync(
    topicArn: "arn:aws:sns:sa-east-1:123456:billing-events",
    endpoint: "arn:aws:sqs:sa-east-1:123456:billing-queue",
    protocol: "sqs"
);
```

---

### MercadoPago API (Payment Processing)

```
Endpoints utilizados:

1. Criar pagamento (POST /v1/payments)
   Request:
   {
     "amount": 100.00,
     "currency_id": "BRL",
     "description": "Serviço de reparo",
     "payment_method_id": "credit_card",
     "payer": {
       "email": "customer@example.com",
       "token": "card-token-123"
     }
   }
   
   Response:
   {
     "id": 12345678,
     "status": "approved|pending|rejected",
     "status_detail": "accredited|pending_review|...",
     "transaction_amount": 100.00
   }

2. Consultar pagamento (GET /v1/payments/{id})
   Usado para: Retry manual, verificar status
   
3. Reembolsar (POST /v1/payments/{id}/refunds)
   Usado para: Cancelamento / Compensação
   
Tratamento de Erro:
├── 400 Bad Request: Validação
├── 401 Unauthorized: Credentials
├── 429 Too Many Requests: Rate limite (retry)
└── 5xx Server Error: Retry exponencial
```

**Configuração em Program.cs**:
```csharp
builder.Services.Configure<MercadoPagoOptions>(options =>
{
    options.AccessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN");
    options.IsSandbox = bool.Parse(Environment.GetEnvironmentVariable("MERCADOPAGO_IS_SANDBOX") ?? "true");
});
```

---

## 💾 Banco de Dados

### PostgreSQL (RDS Free Tier)

```
┌──────────────────────────────────────────┐
│ Database: billingservice_db              │
├──────────────────────────────────────────┤
│                                          │
│ Schemas & Tabelas:                       │
│                                          │
│ public                                   │
│ ├── orcamento                            │
│ │   ├── id (UUID, PK)                    │
│ │   ├── os_id (UUID, FK)                 │
│ │   ├── valor (DECIMAL)                  │
│ │   ├── email_cliente (VARCHAR)          │
│ │   ├── status (VARCHAR) - Enum          │
│ │   ├── correlation_id (UUID)            │
│ │   ├── causation_id (UUID)              │
│ │   ├── criado_em (TIMESTAMPTZ UTC)     │
│ │   ├── atualizado_em (TIMESTAMPTZ UTC) │
│ │   └── Indexes:                         │
│ │       ├── (os_id)                      │
│ │       └── (correlation_id)             │
│ │                                        │
│ ├── pagamento                            │
│ │   ├── id (UUID, PK)                    │
│ │   ├── os_id (UUID, FK)                 │
│ │   ├── orcamento_id (UUID, FK)          │
│ │   ├── valor (DECIMAL)                  │
│ │   ├── metodo (VARCHAR)                 │
│ │   ├── status (VARCHAR)                 │
│ │   ├── mercadopago_id (VARCHAR)         │
│ │   ├── criado_em (TIMESTAMPTZ UTC)     │
│ │   ├── atualizado_em (TIMESTAMPTZ UTC) │
│ │   └── Indexes:                         │
│ │       ├── (os_id, mercadopago_id)      │
│ │       └── (correlation_id)             │
│ │                                        │
│ ├── atualizacao_status_os                │
│ │   ├── id (UUID, PK)                    │
│ │   ├── os_id (UUID, FK)                 │
│ │   ├── status_anterior (VARCHAR)        │
│ │   ├── novo_status (VARCHAR)            │
│ │   ├── motivo (TEXT)                    │
│ │   ├── alterado_por (VARCHAR)           │
│ │   ├── atualizado_em (TIMESTAMPTZ UTC) │
│ │   ├── correlation_id (UUID)            │
│ │   └── Index:                           │
│ │       └── (os_id, atualizado_em DESC)  │
│ │                                        │
│ ├── outbox_message                       │
│ │   ├── id (UUID, PK)                    │
│ │   ├── aggregate_id (UUID)              │
│ │   ├── aggregate_type (VARCHAR)         │
│ │   ├── event_type (VARCHAR)             │
│ │   ├── payload (JSONB) ← Índice!        │
│ │   ├── created_at (TIMESTAMPTZ UTC)    │
│ │   ├── published (BOOLEAN)              │
│ │   ├── published_at (TIMESTAMPTZ UTC)  │
│ │   ├── correlation_id (UUID)            │
│ │   ├── causation_id (UUID)              │
│ │   └── Index:                           │
│ │       └── (published, created_at)      │
│ │                                        │
│ └── inbox_message                        │
│     ├── id (UUID, PK)                    │
│     ├── event_type (VARCHAR)             │
│     ├── payload (JSONB)                  │
│     ├── provider_event_id (VARCHAR, UK)  │
│     ├── received_at (TIMESTAMPTZ UTC)   │
│     ├── correlation_id (UUID)            │
│     ├── causation_id (UUID)              │
│     ├── processed (BOOLEAN)              │
│     ├── processed_at (TIMESTAMPTZ UTC)  │
│     └── Indexes:                         │
│         ├── UNIQUE (provider_event_id)   │
│         └── (processed, received_at)     │
│                                          │
└──────────────────────────────────────────┘

Connection String (Npgsql):
Host=rds-prod.c1234abcd.sa-east-1.rds.amazonaws.com;
Database=billingservice_db;
Username=postgres;
Password=${DB_PASSWORD};
Pooling=true;
MaxPoolSize=20;
```

**Migrações (Entity Framework Core)**:
```bash
# Criar nova migração
dotnet ef migrations add AddOrcamentoTable --output-dir Data/Migrations

# Aplicar migrações
dotnet ef database update

# Rollback
dotnet ef database update <previous-migration>
```

---

## 🔐 Segurança

### Autenticação

```
Esquema: JWT Bearer Token

Request:
GET /api/billing/budgets/123
Authorization: Bearer eyJhbGcOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

Token Payload:
{
  "sub": "user-id",
  "email": "user@example.com",
  "role": "admin",
  "iat": 1629825600,
  "exp": 1629912000
}

Validação:
├─ Signature: Verifica se token foi emitido por autoridade conhecida
├─ Expiration: Verifica se token ainda é válido
└─ IssuedAt: Verifica se foi emitido no passado

Aplicação:
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };
    });

[Authorize]
[HttpPost("budgets/{osId}/approve")]
public async Task<IActionResult> ApproveBudget(Guid osId) { }
```

### Validação de Entrada

```csharp
public class CreateBudgetRequest
{
    [Required]
    public Guid OsId { get; set; }
    
    [Range(0.01, 999999.99)]
    public decimal Valor { get; set; }
    
    [EmailAddress]
    public string EmailCliente { get; set; }
}

[HttpPost("budgets")]
public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetRequest request)
{
    // ValidationFilter captura ModelState inválido
    // Retorna 400 Bad Request com detalhes
}
```

### HTTPS / TLS

```
Portas:
├── 5000 (HTTP) - Apenas desenvolvimento
└── 5001 (HTTPS) - Produção com certificado

Certificado:
├── Desenvolvimento: Self-signed (gerado automaticamente)
└── Produção: Let's Encrypt (via Kubernetes cert-manager)

Configuração:
app.UseHttpsRedirection(); // Redireciona HTTP → HTTPS
```

### Secrets Management

```
Ambiente de Desenvolvimento (Local):
└─ appsettings.Development.json
   └─ Valores fake/não-sensíveis

Ambiente de Staging/Produção:
├─ Kubernetes Secrets:
│  ├─ JWT_KEY
│  ├─ DB_PASSWORD
│  ├─ MERCADOPAGO_ACCESS_TOKEN
│  └─ AWS credenciais (IAM Role preferido)
│
└─ AWS Secrets Manager (alternativa):
   ├─ Rotação automática
   ├─ Auditoria completa
   └─ Acesso temporário com TTL

Aplicação via variáveis de ambiente:
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD")
    ?? throw new InvalidOperationException("Missing DB_PASSWORD");
```

---

## 📊 Observabilidade

### Logging

```
Framework: Serilog com structured logging

Estrutura:
{
  "Timestamp": "2026-02-23T15:30:45.1234567Z",
  "Level": "Information",
  "MessageTemplate": "Orcamento criado: {OsId}",
  "Properties": {
    "OsId": "550e8400-e29b-41d4-a716-446655440000",
    "CorrelationId": "123e4567-e89b-12d3-a456-426614174000",
    "UserId": "user-123",
    "SourceContext": "OFICINACARDOZO.BILLINGSERVICE.Application.OrcamentoService"
  },
  "Exception": null,
  "RenderedMessage": "Orcamento criado: 550e8400..."
}

Destino:
├── Stdout (pod logs)
│   └── Coletado por FluentBit/CloudWatch Container Insights
│
└── CloudWatch Log Group
    └── /eks/prod/billingservice/application
        Retenção: 30 dias (produção)
```

### Distributed Tracing (CorrelationId)

```
Request 1 (Cliente):
└─ GET /api/billing/budgets/550e8400
   ├── CorrelationId: 123e4567 (gerado por CorrelationIdMiddleware)
   └── Fluxo:
       ├─ BillingController
       ├─ OrcamentoService
       ├─ BillingDbContext
       ├─ Publica evento em OutboxMessage
       └─ Logs incluem: CorrelationId = 123e4567

Request 2 (Consumo de evento):
└─ SQS Message (do evento publicado)
   ├── CorrelationId: 123e4567 (propagado da Request 1)
   └── Fluxo:
       ├─ SqsEventConsumerHostedService
       ├─ OsCreatedHandler
       ├─ OrcamentoService
       └─ Logs incluem: CorrelationId = 123e4567

→ Rastreamento end-to-end: Todos os logs com mesmo CorrelationId
```

### Métricas

**Prometheus Endpoints** (custom):
```
GET /metrics

Métricas:
├── http_requests_total[...] - Total de requisições
├── http_request_duration_seconds[...] - Latência
├── outbox_messages_pending - Eventos não publicados
├── inbox_messages_unprocessed - Eventos não processados
├── mercadopago_api_calls_total - Chamadas MercadoPago
├── mercadopago_api_errors_total - Erros MercadoPago
├── database_connection_pool_size - Conexões ativas
└── sqs_messages_received_total - Mensagens SQS recebidas
```

### Health Checks

```
GET /api/health

Response:
{
  "status": "Healthy" | "Degraded" | "Unhealthy",
  "checks": {
    "PostgreSQL": "Healthy",
    "AWS SQS": "Healthy",
    "MercadoPago API": "Degraded",
    "Memory": "Healthy"
  },
  "totalDuration": "150ms"
}

Kubernetes:
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
  initialDelaySeconds: 15
  periodSeconds: 5
```

### Alertas (CloudWatch)

```
Regras:

1. High Error Rate
   └─ IF http_4xx + http_5xx > 5% in 5 minutes
      THEN send SNS alert to devops

2. SQS Message Age
   └─ IF ApproximateAgeOfOldestMessage > 60s
      THEN send SNS alert

3. Database Connection Pool
   └─ IF active_connections > 18 (de 20)
      THEN send SNS alert

4. MercadoPago API Unavailable
   └─ IF mercadopago_errors / total > 10%
      THEN trigger circuit breaker
```

---

## 🚀 Deployment

### Docker

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["OFICINACARDOZO.BILLINGSERVICE.csproj", "./"]
RUN dotnet restore "OFICINACARDOZO.BILLINGSERVICE.csproj"
COPY . .
RUN dotnet build "OFICINACARDOZO.BILLINGSERVICE.csproj" -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000 5001
ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
ENTRYPOINT ["dotnet", "OFICINACARDOZO.BILLINGSERVICE.dll"]
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: billingservice
  namespace: production
spec:
  replicas: 3  # Alta disponibilidade
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  
  template:
    metadata:
      labels:
        app: billingservice
    spec:
      containers:
      - name: billingservice
        image: 123456.dkr.ecr.sa-east-1.amazonaws.com/billingservice:v1.0.0
        
        ports:
        - containerPort: 5000
          name: http
        - containerPort: 5001
          name: https
        
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: Production
        - name: AWS_REGION
          value: sa-east-1
        - name: JWT_KEY
          valueFrom:
            secretKeyRef:
              name: jwt-secret
              key: jwt-key
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: db-secret
              key: password
        
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        
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
          initialDelaySeconds: 15
          periodSeconds: 5
          failureThreshold: 2

---
apiVersion: v1
kind: Service
metadata:
  name: billingservice
  namespace: production
spec:
  type: ClusterIP
  selector:
    app: billingservice
  ports:
  - port: 80
    targetPort: 5000
    protocol: TCP
    name: http
```

### CI/CD Pipeline (GitHub Actions)

```yaml
name: Build & Deploy BillingService

on:
  push:
    branches: [develop, main]
  pull_request:
    branches: [develop, main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      
      - name: Restore
        run: dotnet restore
      
      - name: Build
        run: dotnet build --configuration Release --no-restore
      
      - name: Test
        run: dotnet test --configuration Release --no-build \
             /p:CollectCoverage=true \
             /p:CoverletOutputFormat=cobertura
      
      - name: Upload coverage
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage.cobertura.xml
          flags: unittests
          fail_ci_if_error: true
      
      - name: Build Docker image
        run: docker build -t billingservice:${{ github.sha }} .
      
      - name: Push to ECR
        if: github.ref == 'refs/heads/main'
        run: |
          aws ecr get-login-password --region sa-east-1 | \
            docker login --username AWS --password-stdin $ECR_REGISTRY
          docker tag billingservice:${{ github.sha }} $ECR_IMAGE:latest
          docker push $ECR_IMAGE:latest

  deploy:
    needs: build
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - name: Deploy to EKS
        run: |
          kubectl set image deployment/billingservice \
            billingservice=$ECR_IMAGE:latest \
            -n production
          kubectl rollout status deployment/billingservice -n production
```

---

## 📋 Decisões Arquiteturais

### 1. **Por que Event-Driven?**

| Alternativa | Pros | Cons |
|-----------|------|------|
| **REST API síncrona** | Simples, previsível | Acoplamento forte, difícil escalar |
| **Event-Driven (escolhido)** | Desacoplado, escalável, resiliente | Complexidade operacional, eventual consistency |

**Justificativa**: BillingService não deve bloquear OrderService. Processamento assíncrono permite retry, compensação e evolução independente.

---

### 2. **Por que PostgreSQL em vez de NoSQL?**

| Alternativa | Pros | Cons |
|-----------|------|------|
| **PostgreSQL (escolhido)** | ACID, transações, joins | Um pouco mais lento que NoSQL |
| **DynamoDB** | Serverless, escalável | Sem transações multi-registro, caro |
| **MongoDB** | Flexível, rápido | Sem ACID nativo, complexo para queries |

**Justificativa**: Outbox/Inbox REQUER transações ACID. Relacionamentos entre agregados requerem joins. Dados financeiros = ACID não-negociável.

---

### 3. **Por que Kubernetes em vez de heroku/App Service?**

| Alternativa | Pros | Cons |
|-----------|------|------|
| **Kubernetes (escolhido)** | Controle total, portável | Operacional complexo |
| **Heroku** | Deploy simples, PaaS | Caro para produção, vendor lock-in |
| **Azure App Service** | Integração Microsoft, gerenciado | Vendor lock-in AWS |

**Justificativa**: EKS já é parte da infraestrutura OficinaCardozo. Garante multi-cloud portability e controle operacional.

---

### 4. **Por que Serilog em vez de Application Insights?**

| Alternativa | Pros | Cons |
|-----------|------|------|
| **Serilog → CloudWatch (escolhido)** | Open-source, portável, JSON estruturado | Setup mais manual |
| **Application Insights** | Integração melhor com .NET, dashboards | Vendor lock-in Azure, caro |

**Justificativa**: Infraestrutura AWS. CloudWatch já está presente. Serilog é agnóstico a vendor.

---

### 5. **Por que JWT em vez de OAuth2?**

| Alternativa | Pros | Cons |
|-----------|------|------|
| **JWT (escolhido)** | Simples, stateless, escalável | Revogação de tokens complexa |
| **OAuth2 + Keycloak** | Revogação imediata, AAA centralizado | Mais complexo, requer servidor Keycloak |

**Justificativa**: BillingService é um serviço interno. OAuth2 adiciona complexidade desnecessária. JWT com curta expiração (1h) é suficiente.

---

## 📚 Referências

- [Microservices Patterns - Chris Richardson](https://microservices.io/patterns/index.html)
- [Event Sourcing & CQRS - Greg Young](https://coding-gecko.com/cqrs-pattern/)
- [Transactional Outbox Pattern - SQLAlchemy Docs](https://docs.sqlalchemy.org/)
- [Kubernetes Best Practices - Official Docs](https://kubernetes.io/docs/concepts/configuration/overview/)
- [PostgreSQL Performance Tuning - Official Docs](https://www.postgresql.org/docs/current/performance.html)
- [Saga Pattern for Distributed Transactions](https://microservices.io/patterns/data/saga.html)

---

**Última atualização**: 23 de Fevereiro de 2026  
**Proprietário**: Equipe OficinaCardozo  
**Revisores**: Arquitetura, DevOps, Backend Lead
