# Implementação de Observabilidade - BillingService

📋 **Data**: 22 de fevereiro de 2026  
🎯 **Objetivo**: Replicar padrão de observabilidade (EKS + CloudWatch) com logging estruturado, correlação e eventos de negócio

## ✅ Resumo de Implementação

### 1. Logging Estruturado com Serilog

#### Pacotes NuGet adicionados:
- ✅ `Serilog.AspNetCore` v8.0.1 - Integração automática com ASP.NET
- ✅ `Serilog.Sinks.Console` v5.0.1 - Output em console com JSON
- ✅ `Serilog.Enrichers.Environment` v2.3.0 - Enriquecimento com EnvironmentName
- ✅ `Serilog.Enrichers.Thread` v4.0.0 - Enriquecimento com ThreadId

#### Configuração em Program.cs:
```csharp
var logger = new LoggerConfiguration()
    .Enrich.FromLogContext()           // LogContext (CorrelationId)
    .Enrich.WithEnvironmentName()      // Environment
    .Enrich.WithThreadId()             // ThreadId
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())  // JSON format
    .MinimumLevel.Information()
    .CreateLogger();

builder.Host.UseSerilog();
```

**Comportamento**: Logs em JSON no console, compatível com CloudWatch agent em EKS

### 2. CorrelationId Ponta a Ponta

#### Middleware criado: `CorrelationIdMiddleware.cs`

**Responsabilidades**:
- ✅ Lê header `Correlation-Id` da request
- ✅ Gera GUID se não existir
- ✅ Armazena no `HttpContext.Items`
- ✅ Enriquece logs com `LogContext.PushProperty`
- ✅ Retorna `Correlation-Id` no response header

**Fluxo**:
```
Request Header: Correlation-Id
    ↓
Middleware extrai ou gera GUID
    ↓
LogContext.PushProperty("CorrelationId", value)
    ↓
Todos logs incluem CorrelationId automaticamente
    ↓
Response Header: Correlation-Id
```

**Registrado em Program.cs**:
```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
```

### 3. Logs de Negócio Estruturados

#### 3.1 OutboxProcessor (Publicação de Eventos)
**Arquivo**: `src/Messaging/OutboxProcessor.cs`

```csharp
_logger.LogInformation(
    "🎉 BillingService gerou evento {EventType}. " +
    "Id: {MessageId}, CorrelationId: {CorrelationId}, " +
    "SnsMessageId: {SnsMessageId}, Status: PublicadoComSucesso",
    message.EventType,
    message.Id,
    message.CorrelationId,
    snsMessageId);
```

**Inclui**: EventType, MessageId, CorrelationId, SnsMessageId, Status

#### 3.2 OsCreatedHandler (Consumo de Eventos)
**Arquivo**: `src/Handlers/OsCreatedHandler.cs`

```csharp
// Início do processamento
_logger.LogInformation(
    "📬 BillingService consumiu evento OsCreated. " +
    "OsId: {OsId}, CorrelationId: {CorrelationId}",
    envelope.Payload.OsId,
    envelope.CorrelationId);

// Após criar OutboxMessage
_logger.LogInformation(
    "✅ BillingService gerou OutboxMessage para evento {EventType}. " +
    "MessageId: {MessageId}, OsId: {OsId}, CorrelationId: {CorrelationId}, " +
    "Status: ProntoParaPublicar",
    outboxMessage.EventType,
    outboxMessage.Id,
    envelope.Payload.OsId,
    envelope.CorrelationId);

// Erro
_logger.LogError(
    ex,
    "❌ Erro ao processar OsCreated. OsId: {OsId}, " +
    "CorrelationId: {CorrelationId}, Erro: {ErrorMessage}",
    envelope.Payload.OsId,
    envelope.CorrelationId,
    ex.Message);
```

#### 3.3 SqsEventConsumerHostedService (Consumo da SQS)
**Arquivo**: `src/Handlers/SqsEventConsumerHostedService.cs`

```csharp
// Processamento iniciado
_logger.LogInformation(
    "📬 BillingService processando evento da SQS. " +
    "EventType: {EventType}, CorrelationId: {CorrelationId}, " +
    "MessageId: {MessageId}, CausationId: {CausationId}",
    eventType, correlationId, message.MessageId, causationId);

// Sucesso
_logger.LogInformation(
    "✅ BillingService consumiu evento com sucesso. " +
    "EventType: {EventType}, CorrelationId: {CorrelationId}, " +
    "MessageId: {MessageId}, Status: Processado",
    eventType, correlationId, message.MessageId);

// Erro
_logger.LogError(
    ex,
    "❌ Erro ao processar mensagem da SQS. EventType: {EventType}, " +
    "CorrelationId: {CorrelationId}, MessageId: {MessageId}, " +
    "Erro: {ErrorMessage}",
    eventType ?? "Desconhecido",
    correlationId ?? "N/A",
    message?.MessageId ?? "N/A",
    ex.Message);
```

### 4. Padrão de Confiabilidade

| Operação | Momento do Log | Condição |
|----------|---------------|----------|
| Publicação em SNS | Após `PublishAsync().Result` bem-sucedido | Resposta positiva de AWS |
| Consumo de evento | Após `consumer.ConsumeAsync()` concluído | Processamento completo |
| Outbox criado | Após `SaveChangesAsync()` | Persistência em BD confirmada |

### 5. CloudWatch Log Group

**Criado**: `/eks/prod/billingservice/application`

```bash
# Comando executado
aws logs create-log-group --region sa-east-1 \
    --log-group-name /eks/prod/billingservice/application

# Definir retenção
aws logs put-retention-policy --region sa-east-1 \
    --log-group-name /eks/prod/billingservice/application \
    --retention-in-days 30
```

**Configuração**: Retenção de 30 dias

## 📊 Exemplo de Log Estruturado (JSON)

```json
{
  "Timestamp": "2026-02-22T10:30:45.123Z",
  "Level": "Information",
  "MessageTemplate": "BillingService gerou evento {EventType}. Id: {MessageId}, CorrelationId: {CorrelationId}, Status: PublicadoComSucesso",
  "Properties": {
    "EventType": "BudgetGenerated",
    "MessageId": 12345,
    "CorrelationId": "550e8400-e29b-41d4-a716-446655440000",
    "EnvironmentName": "Production",
    "ThreadId": 7,
    "RequestId": "0HN4A3V8GP7B6:00000001"
  }
}
```

## 🔍 Queries CloudWatch Logs Insights

### 1. Eventos de Negócio (publicação/consumo)
```sql
fields @timestamp, @message
| filter @message like /gerou evento|consumiu evento/
| sort @timestamp desc
| limit 100
```

### 2. Rastreamento por CorrelationId
```sql
fields @timestamp, @message
| filter @message like /CorrelationId/
| stats count() by CorrelationId
| sort count() desc
```

### 3. Tipos de Evento Específicos
```sql
fields @timestamp, @message
| filter @message like /BudgetGenerated|BudgetApproved|PaymentConfirmed/
| stats count() by @message
```

### 4. Erros e Rastreamento
```sql
fields @timestamp, Level, @message, EventType, CorrelationId
| filter Level = "Error"
| sort @timestamp desc
| limit 100
```

## 📁 Arquivos Alterados

| Arquivo | Mudança |
|---------|--------|
| `OFICINACARDOZO.BILLINGSERVICE.csproj` | Adicionados 4 pacotes Serilog |
| `Program.cs` | Configuração Serilog + Middleware CorrelationId |
| `src/API/CorrelationIdMiddleware.cs` | **Novo arquivo** - Middleware de correlação |
| `src/Messaging/OutboxProcessor.cs` | Logs de negócio estruturados |
| `src/Handlers/OsCreatedHandler.cs` | Logs de consumo de evento |
| `src/Handlers/SqsEventConsumerHostedService.cs` | Logs de processamento SQS |

## ✨ Build Status

```
Build succeeded.
    0 Error(s)
    32 Warning(s) (pre-existentes)
```

## 🚀 Próximos Passos para Deploy

### 1. Docker Image
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY bin/Release/net8.0/publish .
ENV AWS_REGION=sa-east-1
ENV CLOUDWATCH_LOG_GROUP=/eks/prod/billingservice/application
ENTRYPOINT ["dotnet", "OFICINACARDOZO.BILLINGSERVICE.dll"]
```

### 2. Deploy no EKS
```yaml
apiVersion: v1
kind: Pod
metadata:
  name: billingservice
spec:
  containers:
  - name: billingservice
    image: billingservice:latest
    env:
    - name: AWS_REGION
      value: sa-east-1
    - name: CLOUDWATCH_LOG_GROUP
      value: /eks/prod/billingservice/application
    # Credenciais via IRSA (IAM Roles for Service Accounts)
```

### 3. CloudWatch Agent (em cada nó EKS)
Os logs STDOUT/STDERR da aplicação serão coletados automaticamente pelo CloudWatch agent rodando no nó.

## 📋 Checklist de Validação

- [x] Pacotes NuGet instalados com sucesso
- [x] Serilog configurado com JSON formatter
- [x] Middleware CorrelationId funcionando
- [x] Logs de negócio em OutboxProcessor
- [x] Logs de consumo em OsCreatedHandler
- [x] Logs de SQS em SqsEventConsumerHostedService
- [x] Build compila sem erros
- [x] CloudWatch Log Group criado
- [x] Retenção definida para 30 dias
- [ ] Deploy em dev/staging (próxima fase)
- [ ] Validação de logs no CloudWatch (após deploy)

## 📝 Notas Importantes

1. **JSON Formatter**: Os logs são saída em JSON para facilitar parsing no CloudWatch e outras ferramentas
2. **LogContext**: CorrelationId é automaticamente incluído em todos os logs através do Serilog LogContext
3. **Enriquecimento**: ThreadId e EnvironmentName ajudam a rastrear execução distribuída
4. **Sem hardcoding de credenciais**: Usa credenciais do pod/container (IRSA em EKS)
5. **Escalável**: Padrão funciona com múltiplas instâncias do microserviço

---

✅ **Implementação concluída em 22/02/2026**
