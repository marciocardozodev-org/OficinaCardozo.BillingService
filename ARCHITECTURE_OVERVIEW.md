# 🏗️ Arquitetura: BillingService no Ecossistema OficinaCardozo

## Visão Geral da Saga

```
┌─────────────┐
│  OSService  │
│ (Microserviço)
└──────┬──────┘
       │
       └──► Evento: "OsCreated"
            └──► SNS Topic: os-created
                 └──► SQS Queue: billing-events
                      └──► BillingService
                           ├─► Cria Orçamento
                           ├─► Salva no DB (Outbox Pattern)
                           └──► Publica: BudgetGenerated
                                └──► SNS Topic: budget-generated
                                     └──► OSService consome
                                          (atualiza estado)
```

## Componentes do BillingService

```
┌────────────────────────────────────────────────────────────────┐
│                      BillingService (.NET)                     │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  ┌──────────────────────┐        ┌──────────────────────┐    │
│  │   REST API (Controllers)    │   │  Event Handlers      │    │
│  │  ┌────────────────────┐     │   │  ┌──────────────────┐   │
│  │  │ POST /orcamento    │     │   │  │ OsCreatedHandler │   │
│  │  │ GET /budgets/{id}  │     │   │  │ OsCanceledHandler│   │
│  │  │ POST /pagamento    │     │   │  └──────────────────┘   │
│  │  └────────────────────┘     │   │                         │
│  └──────────────────────────────┘   └──────────────────────────┘
│                   ▼                             ▼               │
│          ┌────────────────────────────────────────────┐        │
│          │         Application Services               │        │
│          │  ┌──────────────────────────────────────┐ │        │
│          │  │ OrcamentoService                     │ │        │
│          │  │ PagamentoService                    │ │        │
│          │  │ AtualizacaoStatusOsService          │ │        │
│          │  │ ServiceOrchestrator                 │ │        │
│          │  └──────────────────────────────────────┘ │        │
│          └────────────────────┬───────────────────────┘        │
│                               ▼                                │
│          ┌──────────────────────────────────────────┐         │
│          │  Transactional Outbox Pattern           │         │
│          │  ┌────────────────────────────────────┐ │         │
│          │  │ OutboxMessage (evento não enviado) │ │         │
│          │  │ - event_type: BudgetGenerated     │ │         │
│          │  │ - payload: JSON                    │ │         │
│          │  │ - published: false                │ │         │
│          │  │ - correlation_id, causation_id   │ │         │
│          │  └────────────────────────────────────┘ │         │
│          └──────────────────────────────────────────┘         │
│                               ▼                                │
│         ┌────────────────────────────────────────┐            │
│         │  Event Publisher (SQS)                 │            │
│         │  - Publica BudgetGenerated             │            │
│         │  - Publica em SNS (OSService consome)  │            │
│         └────────────────────────────────────────┘            │
│                                                                │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                   Database Layer                       │  │
│  │  ┌──────────────────────────────────────────────────┐ │  │
│  │  │ PostgreSQL (RDS)                                 │ │  │
│  │  │ Tables:                                          │ │  │
│  │  │  - Orcamentos (budgets)                         │ │  │
│  │  │  - Pagamentos (payments)                        │ │  │
│  │  │  - AtualizacoesStatusOs (status updates)       │ │  │
│  │  │  - OutboxMessages (eventos para enviar)        │ │  │
│  │  │  - InboxMessages (eventos recebidos)           │ │  │
│  │  └──────────────────────────────────────────────────┘ │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

## Fluxo de Eventos: "Orçamento Solicitado"

```
1. OSService emite OsCreated
   │
   ├─► SNS Topic: os-created
   │
   └─► SQS Queue: billing-events
       │
       └─► BillingService.SqsEventConsumerImpl
           │
           ├─ Recebe mensagem JSON
           ├─ Deserializa para OsCreated
           ├─ chama OsCreatedHandler.HandleAsync()
           │
           └─► OsCreatedHandler
               │
               ├─ Inject: OrcamentoService, BillingDbContext, IEventPublisher
               │
               ├─► Validação
               │   └─ Verifica se OS já existe
               │
               ├─► Cria novo Orcamento
               │   ├─ osId: GUID da OS
               │   ├─ cliente: info do cliente
               │   ├─ valor: valor padrão baseado em serviço
               │   ├─ status: DRAFT
               │   └─ createdAt: timestamp
               │
               ├─► Salva no DB
               │   ├─ await _db.Orcamentos.AddAsync(orcamento)
               │   └─ await _db.SaveChangesAsync()
               │
               ├─► Cria OutboxMessage (não persiste evento imediatamente)
               │   ├─ event_type: "BudgetGenerated"
               │   ├─ payload: serializado BudgetGenerated event
               │   ├─ correlation_id: propagado de OsCreated
               │   ├─ causation_id: ID do OsCreated
               │   └─ published: false ← CRITICAL!
               │
               ├─► Salva OutboxMessage no DB (MESMA TRANSAÇÃO)
               │   └─ await _db.SaveChangesAsync()
               │
               └─► OutboxProcessor (bg job)
                   │
                   ├─ Polling: A cada 5s verifica DB
                   ├─ Encontra: OutboxMessages onde published=false
                   │
                   └─► Para cada mensagem não publicada:
                       ├─► Deserializa event
                       ├─► Publica em SNS (BudgetGenerated)
                       ├─► Marca: published=true no DB
                       └─► await _db.SaveChangesAsync()

2. BudgetGenerated é consumido por OSService
   │
   ├─► SNS Topic: budget-generated
   │
   └─► SQS Queue: os-events (OSService)
       │
       └─► OSService.handlers
           │
           └─► BudgetGeneratedHandler
               │
               ├─► Atualiza status da OS
               ├─► Notifica cliente
               └─► Persiste em DB do OSService (SEPARADO!)
```

## Stack Tecnológico

### Backend (BillingService)

| Camada | Tecnologia | Versão | Função |
|--------|-----------|--------|--------|
| **Framework** | ASP.NET Core | 8.0 | Web framework |
| **ORM** | Entity Framework Core | 8.0 | Acesso a dados |
| **Database Driver** | Npgsql | Latest | PostgreSQL |
| **Messaging Client** | AWSSDK.SQS | 4.0.2 | Consumir SQS |
| **Auth** | JWT Bearer | Built-in | Autenticação |
| **IoC** | Built-in DI | Built-in | Injeção dependência |
| **JSON** | System.Text.Json | Built-in | Serialização |

### Infrastructure (AWS)

| Serviço | Componente | Função |
|---------|-----------|--------|
| **RDS** | PostgreSQL 14+ | Dados persistentes |
| **SQS** | billing-events | Fila de entrada (OsCreated) |
| **SQS** | billing-events-dlq | Dead Letter Queue |
| **SNS** | budget-generated | Tópico de saída |
| **SNS** | budget-approved | Tópico aprovação |
| **SNS** | payment-* | Tópicos pagamento |
| **EKS** | Kubernetes | Orquestração |
| **ECR** | Docker Registry | Imagens container |
| **IAM** | User COM permissões | Credenciais |

### CI/CD

| Ferramenta | Versão | Função |
|-----------|--------|--------|
| **GitHub Actions** | Built-in | Automação |
| **Terraform** | 1.6.6+ | IaC RDS |
| **kubectl** | Latest (via EKS) | Deploy K8s |
| **Docker** | Latest | Containerização |

## Integração Inter-Serviços

### BillingService ↔ OSService

```
OSService Database       BillingService Database
┌─────────────┐         ┌──────────────────┐
│ Ordens (OS) │         │ Orçamentos       │
├─────────────┤         ├──────────────────┤
│ id          │         │ id                │
│ cliente     │         │ osId (FK) ──────┐│
│ descricao   │         │ cliente          ││ NÃO ACESSA
│ status      │         │ valor            ││ BD DO
│ ...         │         │ status           ││ OSSERVICE!
└─────────────┘         │ ...              ││
                        └──────────────────┘│
                                           │
              Comunicação: Via SQS + SNS ──┘
              (Não compartilham banco!)
```

### Garantias do Design

✅ **Sem Acoplamento de DB**: BillingService tem seu próprio PostgreSQL  
✅ **Assincronia**: Via SQS + SNS (não chamadas REST síncronas)  
✅ **Resiliência**: Outbox pattern garante entrega eventual  
✅ **Idempotência**: InboxMessage deduplica por provider_event_id  
✅ **Rastreabilidade**: CorrelationId + CausationId em todos eventos  
✅ **Compensação**: Handlers de cancelamento para rollback distribuído  

## Deployment: GitOps com GitHub Actions

```
Push para master/homolog
        │
        ├─► GitHub Actions Trigger
        │
        ├─► Build .NET (dotnet build)
        │   └─ Resultado: DLL compilado
        │
        ├─► Test (dotnet test) [Optional]
        │   └─ Validar lógica
        │
        ├─► Docker Build & Push
        │   └─ DockerHub: marciocardozodev/oficinacardozo-billingservice:<tag>
        │
        ├─► Terraform Provision RDS
        │   └─ AWS: PostgreSQL database
        │
        ├─► EKS Configuration
        │   │
        │   ├─ kubectl apply aws-messaging-config.yaml
        │   │  └─ Injeta AWS_REGION, SQS URLs, SNS ARNs
        │   │
        │   ├─ kubectl create secret aws-messaging-secrets
        │   │  └─ Injeta AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY
        │   │
        │   ├─ kubectl apply create-db-job.yaml
        │   │  └─ Cria tabelas (migrations)
        │   │
        │   └─ kubectl apply deployment.yaml
        │      └─ Deploy BillingService pod
        │
        └─► Sucesso! ✅ BillingService rodando
            └─ Esperando eventos de OsCreated em SQS
```

## Arquivos Principais

```
BillingService/
├── Program.cs                          (DI + AWS config)
├── BillingDbContext.cs                 (EF Core + Outbox/Inbox)
│
├── src/
│   ├── Domain/
│   │   ├── OrderStatusUpdate.cs        (Dados da atualização)
│   │   ├── Payment.cs                  (Dados do pagamento)
│   │   └── Quote.cs                    (Orçamento)
│   │
│   ├── Contracts/Events/
│   │   ├── Input/                      (Eventos que recebe)
│   │   │   ├── OsCreated.cs
│   │   │   ├── OsCanceled.cs
│   │   │   └── OsCompensationRequested.cs
│   │   │
│   │   ├── Output/                     (Eventos que emite)
│   │   │   ├── BudgetGenerated.cs
│   │   │   ├── BudgetApproved.cs
│   │   │   ├── BudgetRejected.cs
│   │   │   ├── PaymentConfirmed.cs
│   │   │   ├── PaymentFailed.cs
│   │   │   └── PaymentReversed.cs
│   │   │
│   │   └── EventEnvelope.cs            (Wrapper com correlation_id)
│   │
│   ├── Messaging/
│   │   ├── SqsEventPublisher.cs        (Publicar em SNS)
│   │   ├── SqsEventConsumerImpl.cs      (Consumir SQS)
│   │   ├── OutboxMessage.cs            (Modelo Outbox)
│   │   ├── InboxMessage.cs             (Modelo Inbox)
│   │   └── IEventPublisher.cs          (Interface)
│   │
│   ├── Handlers/
│   │   ├── OsCreatedHandler.cs         (⭐ Core: criar orçamento)
│   │   ├── OsCanceledHandler.cs        (Compensation)
│   │   └── OsCompensationRequestedHandler.cs (Compensation)
│   │
│   ├── Application/
│   │   ├── OrcamentoService.cs         (Criar orçamento)
│   │   ├── PagamentoService.cs         (Processar pagamento)
│   │   └── AtualizacaoStatusOsService.cs (Atualizar status)
│   │
│   └── API/
│       ├── BillingController.cs        (POST /orcamento)
│       ├── BudgetController.cs         (GET /budgets)
│       └── MercadoPagoService.cs       (Mocks pagamento)
│
├── deploy/k8s/
│   ├── aws-messaging-config.yaml       (ConfigMap público)
│   ├── deployment.yaml                 (Pod + containers)
│   ├── service.yaml                    (Service K8s)
│   ├── create-db-job.yaml              (Migration job)
│   └── secret.yaml                     (Secret genéricos)
│
├── infra/terraform/
│   ├── main.tf                         (RDS provisioning)
│   ├── backend.tf                      (S3 state)
│   └── terraform.tfvars                (Variáveis)
│
└── .github/workflows/
    └── ci-cd-billingservice.yml        (GitHub Actions pipeline)
```

## Métricas de Sucesso

| Métrica | Objetivo | Como Validar |
|---------|----------|--------------|
| **Build Success** | 0 erros de compilação | `dotnet build` ✅ Sucesso |
| **Container Deploy** | Pod em Running state | `kubectl get pods` |
| **DB Connection** | Conectar RDS | `kubectl logs deployment/billingservice` |
| **SQS Consumption** | Receber OsCreated | CloudWatch Metrics |
| **Event Publishing** | Publicar BudgetGenerated | CloudWatch Metrics |
| **Outbox Ingestion** | Eventos no DB | Query `SELECT * FROM outbox_message` |
| **Inbox Dedup** | Sem duplicatas | Query `SELECT * FROM inbox_message` |
| **API Availability** | GET /budgets funciona | `curl localhost:5000/budgets/...` |
| **Latência E2E** | OsCreated → BudgetGenerated | CloudWatch Logs |

---

**Status de Implementação:**
- ✅ Arquitetura desenhada
- ✅ Event contracts implementados
- ✅ Handlers implementados
- ✅ Database persistence implementado
- ✅ Kubernetes YAML atualizado (ConfigMap + Secret pattern)
- ✅ CI/CD pipeline pronto
- ⏳ GitHub Secrets configurados (próximo passo)
- ⏳ Teste E2E (após push)

**Próximo passo:** Configure GitHub Secrets e faça push para master!
