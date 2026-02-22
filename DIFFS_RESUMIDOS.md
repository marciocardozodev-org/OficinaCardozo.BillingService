# Diffs Resumidos - Implementação de Observabilidade

## 1. OFICINACARDOZO.BILLINGSERVICE.csproj

**Adições**: 4 novos PackageReference

```diff
+ <PackageReference Include="Serilog.AspNetCore" Version="8.0.1" />
+ <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
+ <PackageReference Include="Serilog.Enrichers.Environment" Version="2.3.0" />
+ <PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
```

---

## 2. Program.cs

### 2.1 Using Statements (Adições)
```diff
+ using Serilog;
+ using Serilog.Context;
+ using OFICINACARDOZO.BILLINGSERVICE.API;
```

### 2.2 Configuração Serilog (Novo bloco)
```diff
+ // ========== SERILOG CONFIGURATION ==========
+ var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? "sa-east-1";
+ var cloudWatchLogGroup = Environment.GetEnvironmentVariable("CLOUDWATCH_LOG_GROUP") 
+     ?? "/eks/prod/billingservice/application";
+ 
+ var logger = new LoggerConfiguration()
+     .Enrich.FromLogContext()
+     .Enrich.WithEnvironmentName()
+     .Enrich.WithThreadId()
+     .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
+     .MinimumLevel.Information()
+     .CreateLogger();
+ 
+ Serilog.Log.Logger = logger;
+ builder.Host.UseSerilog();
```

### 2.3 Middleware de CorrelationId (Novo)
```diff
+ // Middleware de CorrelationId (deve estar antes de ExceptionHandlingMiddleware)
+ app.UseMiddleware<CorrelationIdMiddleware>();
```

### 2.4 Log de Inicialização (Novo)
```diff
+ var configLogger = app.Services.GetRequiredService<Serilog.ILogger>();
+ configLogger.Information("🚀 BillingService iniciado. CloudWatch Log Group: {CloudWatchLogGroup}", 
+     cloudWatchLogGroup);
+ configLogger.Information("AWS Region: {AwsRegion}, Queue: {QueueUrl}", awsRegion, sqsQueueUrl);
```

---

## 3. src/API/CorrelationIdMiddleware.cs

**Arquivo Novo** - ~60 linhas

Principais funcionalidades:
- Lê header `Correlation-Id` ou gera GUID
- Armazena em `HttpContext.Items`
- Enriquece logs com `LogContext.PushProperty`
- Retorna `Correlation-Id` no response header
- Inclui helper de extensão `GetCorrelationId()`

---

## 4. src/Messaging/OutboxProcessor.cs

### 4.1 Log de Publicação Bem-sucedida

```diff
- _logger.LogInformation(
-     "✅ OutboxMessage {MessageId} ({EventType}) publicada com sucesso. " +
-     "CorrelationId: {CorrelationId}. SnsMessageId: {SnsMessageId}",
-     message.Id,
-     message.EventType,
-     message.CorrelationId,
-     snsMessageId);

+ _logger.LogInformation(
+     "🎉 BillingService gerou evento {EventType}. " +
+     "Id: {MessageId}, CorrelationId: {CorrelationId}, " +
+     "SnsMessageId: {SnsMessageId}, Status: PublicadoComSucesso",
+     message.EventType,
+     message.Id,
+     message.CorrelationId,
+     snsMessageId);
```

**Mudanças**:
- Linguagem mais clara de negócio ("gerou evento")
- Incluído identificador de Status explícito
- Ordenação lógica das propriedades

---

## 5. src/Handlers/OsCreatedHandler.cs

### 5.1 Log de Consumo do Evento

```diff
- _logger.LogInformation(
-     "Processando OsCreated para OS {OsId} com CorrelationId {CorrelationId}",
-     envelope.Payload.OsId,
-     envelope.CorrelationId);

+ _logger.LogInformation(
+     "📬 BillingService consumiu evento OsCreated. " +
+     "OsId: {OsId}, CorrelationId: {CorrelationId}",
+     envelope.Payload.OsId,
+     envelope.CorrelationId);
```

### 5.2 Log de OutboxMessage Criado

```diff
- _logger.LogInformation(
-     "OutboxMessage criada com ID {MessageId} para evento {EventType}",
-     outboxMessage.Id,
-     outboxMessage.EventType);

+ _logger.LogInformation(
+     "✅ BillingService gerou OutboxMessage para evento {EventType}. " +
+     "MessageId: {MessageId}, OsId: {OsId}, CorrelationId: {CorrelationId}, " +
+     "Status: ProntoParaPublicar",
+     outboxMessage.EventType,
+     outboxMessage.Id,
+     envelope.Payload.OsId,
+     envelope.CorrelationId);
```

### 5.3 Log de Erro

```diff
- _logger.LogError(
-     ex,
-     "Erro ao processar OsCreated para OS {OsId}",
-     envelope.Payload.OsId);

+ _logger.LogError(
+     ex,
+     "❌ Erro ao processar OsCreated. OsId: {OsId}, " +
+     "CorrelationId: {CorrelationId}, Erro: {ErrorMessage}",
+     envelope.Payload.OsId,
+     envelope.CorrelationId,
+     ex.Message);
```

---

## 6. src/Handlers/SqsEventConsumerHostedService.cs

### 6.1 Scoping de Variáveis (Novo)

```diff
  private async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
  {
+     string? eventType = null;
+     string? correlationId = null;
+     string? causationId = null;
+ 
      try
      {
```

**Motivo**: Tornar variáveis acessíveis no catch block para logs de erro

### 6.2 Log de Processamento Iniciado

```diff
- _logger.LogInformation($"✓ Evento extraído: Type={eventType}, CorrelationId={correlationId}");

+ _logger.LogInformation(
+     "📬 BillingService processando evento da SQS. " +
+     "EventType: {EventType}, CorrelationId: {CorrelationId}, " +
+     "MessageId: {MessageId}, CausationId: {CausationId}",
+     eventType, correlationId, message.MessageId, causationId);
```

### 6.3 Log de Sucesso

```diff
- _logger.LogInformation($"✓ Evento {eventType} processado com sucesso");

+ _logger.LogInformation(
+     "✅ BillingService consumiu evento com sucesso. " +
+     "EventType: {EventType}, CorrelationId: {CorrelationId}, " +
+     "MessageId: {MessageId}, Status: Processado",
+     eventType, correlationId, message.MessageId);
```

### 6.4 Log de Erro

```diff
- catch (Exception ex)
- {
-     _logger.LogError(ex, "Erro ao processar mensagem SQS");
- }

+ catch (Exception ex)
+ {
+     _logger.LogError(
+         ex,
+         "❌ Erro ao processar mensagem da SQS. EventType: {EventType}, " +
+         "CorrelationId: {CorrelationId}, MessageId: {MessageId}, " +
+         "Erro: {ErrorMessage}",
+         eventType ?? "Desconhecido",
+         correlationId ?? "N/A",
+         message?.MessageId ?? "N/A",
+         ex.Message);
+ }
```

---

## Resumo Estatístico de Mudanças

| Métrica | Valor |
|---------|-------|
| Arquivos criados | 2 |
| Arquivos modificados | 5 |
| Pacotes NuGet adicionados | 4 |
| Linhas adicionadas (logs) | ~25 |
| New Middleware classes | 1 |
| Enriquecimentos Serilog | 3 (LogContext, Environment, Thread) |
| Variáveis de Ambiente | 2 (AWS_REGION, CLOUDWATCH_LOG_GROUP) |

---

## Validação de Build

```bash
$ dotnet build OFICINACARDOZO.BILLINGSERVICE.csproj

Build succeeded.
    0 Error(s)
    32 Warning(s) (pre-existentes)
```

---

## Comandos de Deploy

```bash
# Build da imagem Docker
docker build -t billingservice:latest .

# Push para registry
aws ecr get-login-password --region sa-east-1 | docker login --username AWS --password-stdin <account-id>.dkr.ecr.sa-east-1.amazonaws.com
docker tag billingservice:latest <account-id>.dkr.ecr.sa-east-1.amazonaws.com/billingservice:latest
docker push <account-id>.dkr.ecr.sa-east-1.amazonaws.com/billingservice:latest

# Deploy em K8s/EKS
kubectl apply -f deploy/k8s/deployment.yaml
kubectl rollout status deployment/billingservice -n default
```

---

**Status**: ✅ Pronto para Deploy
**Data**: 22/02/2026
