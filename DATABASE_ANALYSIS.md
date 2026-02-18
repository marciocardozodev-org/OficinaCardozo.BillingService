# 🔍 Análise Crítica: Database e Fluxo de Persistência

## ❌ Problemas Encontrados

### 1️⃣ **CRÍTICO: create-db-job.yaml INCOMPLETO**

**Problema:** Faltam as tabelas de `outbox_message` e `inbox_message`

```sql
-- ❌ FALTAM ESTAS TABELAS
CREATE TABLE outbox_message (...)
CREATE TABLE inbox_message (...)
```

**Impacto:** 
- ❌ BillingDbContext tenta usar `DbSet<OutboxMessage>` mas tabela não existe
- ❌ InboxMessage também não tem tabela
- ❌ Job de DB vai falhar

---

### 2️⃣ **CRÍTICO: Mismatch GUID ↔ INT**

**Problema atual:**
```csharp
// Models usam INT
public class Orcamento {
    public int OrdemServicoId { get; set; }  // ❌ INT
}

// Mas OsCreated usa GUID
public class OsCreated {
    public Guid OsId { get; set; }  // ✅ GUID
}

// Handler faz conversão PERIGOSA
int osIdAsInt = Convert.ToInt32(
    envelope.Payload.OsId.GetHashCode() % int.MaxValue  // ❌ RUIM!
);
```

**Por que é ruim:**
- ❌ `GetHashCode()` pode mudar entre execuções
- ❌ Perde rastreabilidade (não há correlação clara entre OsId e OrdemServicoId)
- ❌ Colisão de hashes (2 GUIDs diferentes podem virar o mesmo INT)
- ❌ Impossível fazer JOIN nas queries depois
- ❌ Dificulta auditorias

**Solução:** Usar GUID de ponta a ponta!

---

### 3️⃣ **CRÍTICO: Padrão Transactional Outbox VIOLADO**

**Implementação atual:**
```csharp
public async Task HandleAsync(EventEnvelope<OsCreated> envelope) {
    // 1. Criar orçamento
    var orcamento = await _orcamentoService.GerarEEnviarOrcamentoAsync(...);
    
    // 2. Adicionar OutboxMessage
    var outboxMessage = new OutboxMessage { ... };
    _db.Set<OutboxMessage>().Add(outboxMessage);
    await _db.SaveChangesAsync();  // ← Salva no BD
    
    // 3. PUBLICAR IMEDIATAMENTE
    await _publisher.PublishAsync(budgetGeneratedEnvelope);  // ❌ VIOLAÇÃO!
}
```

**O problema:**
- ❌ Publica LOGO APÓS salvar no BD
- ❌ Se publicação falhar após SaveChanges(), evento perdido
- ❌ Se publicação falhar, não há retry automático
- ❌ Viola o propósito do Transactional Outbox

**Padrão Correto:**
```
Fase 1 (Handler):
  ├─ Salvar Orcamento
  ├─ Salvar OutboxMessage (published: false)
  └─ DB SaveChangesAsync (TRANSAÇÃO ÚNICA)

Fase 2 (Background Job - OutboxProcessor):
  ├─ Query: SELECT * FROM outbox_message WHERE published = false
  ├─ Para cada mensagem:
  │  ├─ Publicar em SNS/SQS
  │  ├─ Update SET published = true
  │  └─ SaveChangesAsync
  └─ Retry automático se falhar
```

---

### 4️⃣ **IMPORTANTE: Campos de Rastreamento Faltam**

**Tabela `orcamento` atual:**
```sql
CREATE TABLE orcamento (
  id INTEGER PRIMARY KEY,
  ordem_servico_id INTEGER,        -- ❌ Deveria ser UUID
  valor NUMERIC(12,2),
  email_cliente VARCHAR(255),
  status SMALLINT,
  criado_em TIMESTAMP
  -- ❌ FALTAM:
  -- correlation_id UUID,   (para rastreamento distribuído)
  -- causation_id UUID,     (para saber qual evento causou)
  -- provider_event_id UUID (para dedup no Inbox)
);
```

**Tabela `atualizacao_status_os` atual:**
```sql
-- ❌ FALTAM campos de rastreamento
-- correlation_id, causation_id, event_type
-- Impossível saber qual evento causou atualização
```

---

### 5️⃣ **IMPORTANTE: Services NÃO salvam OutboxMessage**

**OrcamentoService:**
```csharp
public async Task<Orcamento> GerarEEnviarOrcamentoAsync(...) {
    var orcamento = new Orcamento { ... };
    _db.Orcamentos.Add(orcamento);
    await _db.SaveChangesAsync();  // ✅ Salva orcamento
    // ❌ Mas não lida com Outbox!
    return orcamento;
}
```

**Responsabilidade estar no Handler é OK, MAS:**
- ❌ Se o Handler falhar DEPOIS de criar orcamento, Outbox será criado fora de transação
- ❌ Se SaveChangesAsync do Outbox falhar, orcamento já foi salvo
- ✅ Solução: Usar SaveChangesAsync UMA VEZ por transação completa

---

## ✅ Mapeamento Correto (Recomendado)

### Database Schema Correto

```sql
-- === TABELAS DE NEGÓCIO ===
CREATE TABLE orcamento (
  id BIGSERIAL PRIMARY KEY,                      -- PK interno
  os_id UUID NOT NULL UNIQUE,                    -- FK para ordem de serviço
  valor NUMERIC(12,2) NOT NULL,
  email_cliente VARCHAR(255) NOT NULL,
  status SMALLINT NOT NULL DEFAULT 0,            -- 0:pendente, 1:enviado, 2:aprovado, 3:rejeitado
  correlation_id UUID NOT NULL,                  -- Rastreamento distribuído
  causation_id UUID NOT NULL,                    -- Qual evento causou
  criado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  atualizado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_orcamento_os FOREIGN KEY (os_id) REFERENCES ordem_servico(id)
);
CREATE INDEX idx_orcamento_os_id ON orcamento(os_id);
CREATE INDEX idx_orcamento_correlation_id ON orcamento(correlation_id);

CREATE TABLE pagamento (
  id BIGSERIAL PRIMARY KEY,
  os_id UUID NOT NULL,
  orcamento_id BIGINT NOT NULL REFERENCES orcamento(id),
  valor NUMERIC(12,2) NOT NULL,
  metodo VARCHAR(100) NOT NULL,
  status SMALLINT NOT NULL DEFAULT 0,            -- 0:pendente, 1:confirmado, 2:falhou
  provider_payment_id VARCHAR(255),              -- ID do Mercado Pago
  correlation_id UUID NOT NULL,
  causation_id UUID NOT NULL,
  criado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  atualizado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_pagamento_os_id ON pagamento(os_id);
CREATE INDEX idx_pagamento_orcamento_id ON pagamento(orcamento_id);

CREATE TABLE atualizacao_status_os (
  id BIGSERIAL PRIMARY KEY,
  os_id UUID NOT NULL,
  novo_status VARCHAR(100) NOT NULL,
  event_type VARCHAR(255) NOT NULL,              -- BudgetGenerated, PaymentConfirmed, etc
  correlation_id UUID NOT NULL,
  causation_id UUID NOT NULL,
  atualizado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_atualizacao_os_id ON atualizacao_status_os(os_id);
CREATE INDEX idx_atualizacao_correlation_id ON atualizacao_status_os(correlation_id);

-- === TABELAS DE MESSAGING (Outbox/Inbox Pattern) ===
CREATE TABLE outbox_message (
  id BIGSERIAL PRIMARY KEY,
  event_type VARCHAR(255) NOT NULL,
  payload JSONB NOT NULL,                        -- Usar JSONB para melhor performance
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  published BOOLEAN NOT NULL DEFAULT false,
  published_at TIMESTAMP,
  correlation_id UUID NOT NULL,
  causation_id UUID NOT NULL
);
CREATE INDEX idx_outbox_published ON outbox_message(published, created_at);
CREATE INDEX idx_outbox_correlation_id ON outbox_message(correlation_id);

CREATE TABLE inbox_message (
  id BIGSERIAL PRIMARY KEY,
  event_type VARCHAR(255) NOT NULL,
  payload JSONB NOT NULL,
  received_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  provider_event_id UUID NOT NULL UNIQUE,        -- Para deduplicação!
  correlation_id UUID NOT NULL,
  causation_id UUID NOT NULL
);
CREATE INDEX idx_inbox_provider_event_id ON inbox_message(provider_event_id);
CREATE INDEX idx_inbox_correlation_id ON inbox_message(correlation_id);

-- === AUDITORIAS (Opcional) ===
CREATE TABLE event_audit_log (
  id BIGSERIAL PRIMARY KEY,
  event_type VARCHAR(255) NOT NULL,
  aggregate_type VARCHAR(255) NOT NULL,          -- 'orcamento', 'pagamento', etc
  aggregate_id UUID NOT NULL,
  correlation_id UUID NOT NULL,
  causation_id UUID NOT NULL,
  happened_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_audit_log FOREIGN KEY (correlation_id) REFERENCES outbox_message(correlation_id)
);
```

---

## 📊 Mapeamento Entity Framework

### Models precisam ser corrigidos:

```csharp
// ❌ ATUAL - ERRADO
public class Orcamento {
    public int Id { get; set; }
    public int OrdemServicoId { get; set; }      // ❌ INT!
    //...
}

// ✅ CORRETO
public class Orcamento {
    public long Id { get; set; }                 // PK interno (BIGSERIAL)
    public Guid OsId { get; set; }               // ✅ GUID para OS
    public decimal Valor { get; set; }
    public string EmailCliente { get; set; }
    public StatusOrcamento Status { get; set; }
    public Guid CorrelationId { get; set; }      // Rastreamento
    public Guid CausationId { get; set; }        // Causalidade
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

// ✅ OutboxMessage - Não precisa de mudança, está OK
public class OutboxMessage {
    public long Id { get; set; }
    public string EventType { get; set; }
    public string Payload { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Published { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
}

// ✅ InboxMessage - OK
public class InboxMessage {
    public long Id { get; set; }
    public string EventType { get; set; }
    public string Payload { get; set; }
    public DateTime ReceivedAt { get; set; }
    public Guid ProviderEventId { get; set; }    // Para dedup
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
}
```

---

## 🔄 Fluxo Correto de Persistência

### Fase 1: OsCreated chega no SQS

```
OSService emite OsCreated
  └─ SQS: billing-events
     └─ SqsEventConsumerImpl deserializa
        └─ OsCreatedHandler.HandleAsync(envelope)
```

### Fase 2: Handler cria Orcamento + OutboxMessage (TRANSAÇÃO ÚNICA)

```csharp
public async Task HandleAsync(EventEnvelope<OsCreated> envelope) {
    // 1. Validar
    if (await _db.Orcamentos.AnyAsync(o => o.OsId == envelope.Payload.OsId)) {
        return; // Já processado (inbox dedup)
    }

    // 2. Criar Orcamento
    var orcamento = new Orcamento {
        OsId = envelope.Payload.OsId,                    // ✅ GUID
        Valor = 100.00m,
        EmailCliente = envelope.Payload.ClientEmail,
        Status = StatusOrcamento.Enviado,
        CorrelationId = envelope.CorrelationId,          // ✅ Rastreamento
        CausationId = envelope.CausationId,              // ✅ Causalidade
        CriadoEm = DateTime.UtcNow
    };
    _db.Orcamentos.Add(orcamento);

    // 3. Criar OutboxMessage (MESMO contexto)
    var budgetGenerated = new BudgetGenerated {
        OsId = envelope.Payload.OsId,
        BudgetId = orcamento.Id,
        Amount = orcamento.Valor,
        Status = BudgetStatus.Generated
    };

    var outboxMessage = new OutboxMessage {
        Id = Guid.NewGuid(),
        EventType = nameof(BudgetGenerated),
        Payload = JsonSerializer.Serialize(budgetGenerated),
        CreatedAt = DateTime.UtcNow,
        Published = false,                               // ✅ NÃO PUBLICADO YET
        CorrelationId = envelope.CorrelationId,
        CausationId = Guid.NewGuid()                     // Novo ID para BudgetGenerated
    };
    _db.OutboxMessages.Add(outboxMessage);

    // 4. Salvar tudo UMA VEZ (transação atômica)
    await _db.SaveChangesAsync();

    // ✅ PARAR AQUI! Não publicar ainda!
    // BackgroundService vai cuidar disso
}
```

### Fase 3: OutboxProcessor Background Job (Separado)

```csharp
public class OutboxProcessor : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                // 1. Query mensagens não publicadas
                var unpublished = await _db.OutboxMessages
                    .Where(m => !m.Published)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync(stoppingToken);

                // 2. Para cada mensagem
                foreach (var message in unpublished) {
                    try {
                        // 3. Publicar em SNS/SQS
                        await _publisher.PublishAsync(message);

                        // 4. Marcar como publicado
                        message.Published = true;
                        message.PublishedAt = DateTime.UtcNow;
                        _db.OutboxMessages.Update(message);

                        await _db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex) {
                        // Logging e retry automático
                        _logger.LogError(ex, "Erro ao publicar outbox message {MessageId}", message.Id);
                        // NÃO marca como published - vai tentar novamente na próxima execução
                    }
                }

                // 5. Aguardar antes de próxima verificação (5-10s)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Erro no OutboxProcessor");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
```

### Fase 4: OSService consome BudgetGenerated

```
BudgetGenerated publicado
  └─ SNS: budget-generated
     └─ SQS: os-events (OSService)
        └─ OSService.handlers
           └─ Atualiza BD DELE (não acessa BillingService BD)
```

---

## 📋 Checklist de Correção

- [ ] **create-db-job.yaml**: Adicionar CREATE TABLE outbox_message
- [ ] **create-db-job.yaml**: Adicionar CREATE TABLE inbox_message
- [ ] **Orcamento model**: Mudar OrdemServicoId (int) → OsId (Guid)
- [ ] **Orcamento model**: Adicionar CorrelationId e CausationId
- [ ] **Orcamento model**: Adicionar AtualizadoEm
- [ ] **Pagamento model**: Mudar OrdemServicoId (int) → OsId (Guid)
- [ ] **Pagamento model**: Adicionar orcamento_id FK
- [ ] **Pagamento model**: Adicionar CorrelationId, CausationId
- [ ] **AtualizacaoStatusOs**: Mudar OrdemServicoId (int) → OsId (Guid)
- [ ] **AtualizacaoStatusOs**: Adicionar event_type
- [ ] **OsCreatedHandler**: REMOVER chamada para `_publisher.PublishAsync()`
- [ ] **OrcamentoService**: Não lidar com Outbox (responsabilidade do Handler)
- [ ] **OutboxProcessor**: Implementar background job
- [ ] **DbContext**: Atualizar mapeamentos com novos campos
- [ ] **BillingDbContext**: Criar migrations/indices

---

## 🎯 Resultado Final

Após correções:
- ✅ Rastreamento completo com CorrelationId/CausationId
- ✅ Sem gaps entre persistência e publicação
- ✅ Idempotência garantida pelo Inbox
- ✅ Retry automático de eventos falhados
- ✅ Auditoria via event_audit_log
- ✅ Sem conversão perigosa Guid→Int
- ✅ Alinhado com padrões enterprise (Saga, Outbox)
