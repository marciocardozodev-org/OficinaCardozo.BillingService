-- Script de criação das tabelas principais do BillingService
-- Executado via Kubernetes Job (create-db-job)

-- DROP de tabelas antigas (IF EXISTS para idempotência)
DROP TABLE IF EXISTS inbox_message CASCADE;
DROP TABLE IF EXISTS outbox_message CASCADE;
DROP TABLE IF EXISTS atualizacao_status_os CASCADE;
DROP TABLE IF EXISTS pagamento CASCADE;
DROP TABLE IF EXISTS orcamento CASCADE;

-- Tabela: orcamento (Quotes)
CREATE TABLE orcamento (
    id BIGSERIAL PRIMARY KEY,
    os_id UUID NOT NULL UNIQUE,
    valor NUMERIC(12,2) NOT NULL,
    email_cliente VARCHAR(255) NOT NULL,
    status SMALLINT NOT NULL DEFAULT 0, -- 0: Pendente, 1: Enviado, 2: Aprovado, 3: Rejeitado
    correlation_id UUID NOT NULL,
    causation_id UUID NOT NULL,
    criado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    atualizado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_orcamento_os_id ON orcamento(os_id);
CREATE INDEX idx_orcamento_correlation_id ON orcamento(correlation_id);

-- Tabela: pagamento (Payments)
CREATE TABLE pagamento (
    id BIGSERIAL PRIMARY KEY,
    os_id UUID NOT NULL,
    orcamento_id BIGINT NOT NULL REFERENCES orcamento(id),
    provider_payment_id VARCHAR(255),
    valor NUMERIC(12,2) NOT NULL,
    metodo VARCHAR(100) NOT NULL,
    status SMALLINT NOT NULL DEFAULT 0, -- 0: Pendente, 1: Confirmado, 2: Falhou
    correlation_id UUID NOT NULL,
    causation_id UUID NOT NULL,
    criado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    atualizado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_pagamento_os_id ON pagamento(os_id);
CREATE INDEX idx_pagamento_orcamento_id ON pagamento(orcamento_id);
CREATE INDEX idx_pagamento_correlation_id ON pagamento(correlation_id);

-- Tabela: atualizacao_status_os (Order Status Updates)
CREATE TABLE atualizacao_status_os (
    id BIGSERIAL PRIMARY KEY,
    os_id UUID NOT NULL,
    novo_status VARCHAR(100) NOT NULL,
    event_type VARCHAR(100),
    correlation_id UUID,
    causation_id UUID,
    atualizado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_atualizacao_status_os_os_id ON atualizacao_status_os(os_id);
CREATE INDEX idx_atualizacao_status_os_correlation_id ON atualizacao_status_os(correlation_id);

-- Tabela: outbox_message (Transactional Outbox Pattern)
-- Usado para armazenar eventos que precisam ser publicados
CREATE TABLE outbox_message (
    id BIGSERIAL PRIMARY KEY,
    aggregate_id UUID NOT NULL,
    aggregate_type VARCHAR(255) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    payload TEXT NOT NULL, -- JSON serialized
    correlation_id UUID NOT NULL,
    causation_id UUID NOT NULL,
    published BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    published_at TIMESTAMP
);
CREATE INDEX idx_outbox_message_published ON outbox_message(published);
CREATE INDEX idx_outbox_message_created_at ON outbox_message(created_at);
CREATE INDEX idx_outbox_message_correlation_id ON outbox_message(correlation_id);

-- Tabela: inbox_message (Idempotency - Deduplication)
-- Trackeia quais eventos já foram processados para evitar duplicação
CREATE TABLE inbox_message (
    id BIGSERIAL PRIMARY KEY,
    provider_event_id VARCHAR(255) NOT NULL UNIQUE,
    event_type VARCHAR(255) NOT NULL,
    payload TEXT NOT NULL, -- JSON serialized
    correlation_id UUID NOT NULL,
    causation_id UUID NOT NULL,
    processed BOOLEAN NOT NULL DEFAULT FALSE,
    received_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at TIMESTAMP
);
CREATE INDEX idx_inbox_message_processed ON inbox_message(processed);
CREATE INDEX idx_inbox_message_provider_event_id ON inbox_message(provider_event_id);
CREATE INDEX idx_inbox_message_correlation_id ON inbox_message(correlation_id);
