# Validacao do Fluxo Automatico

Este roteiro valida o fluxo automatico OsCreated -> BudgetApproved -> PaymentConfirmed.

## Pre-requisitos

- BillingService em execucao.
- OutboxProcessor rodando.
- Permissoes AWS configuradas para SNS/SQS.

## Checklist de rollout (quando a tag chegar)

1) Atualizar a imagem no deployment

kubectl set image deployment/billingservice billingservice=marciocardozodev/oficinacardozo-billingservice:<TAG>

2) Aguardar rollout

kubectl rollout status deployment/billingservice

3) Validar pods

kubectl get pods -l app=billingservice

## Validacao rapida (apos rollout)

1) Verificar servicos background

kubectl logs -l app=billingservice --tail=200 | grep -E "SqsEventConsumer|OutboxProcessor"

## Passo 1 - Publicar OsCreated

Exemplo de payload (SNS -> SQS):

{
  "EventType": "OsCreated",
  "Payload": {
    "osId": "00000000-0000-0000-0000-000000000024",
    "description": "Teste fluxo automatico",
    "createdAt": "2026-02-21T12:00:00Z"
  },
  "CorrelationId": "11111111-1111-1111-1111-111111111111",
  "CausationId": "22222222-2222-2222-2222-222222222222"
}

## Passo 2 - Verificar orcamento

Opcao A - API

GET /api/billing/budgets/{osId}

Esperado: orcamento com status Aprovado.

Opcao B - Banco

SELECT id, os_id, status FROM orcamento WHERE os_id = '<osId>';

## Passo 3 - Verificar Outbox

SELECT id, event_type, published FROM outbox_message
WHERE aggregate_id = '<osId>'
ORDER BY created_at DESC;

Esperado:
- BudgetGenerated
- BudgetApproved
- PaymentConfirmed

## Passo 4 - Verificar logs

Procure por:
- "Processando OsCreated"
- "Orcamento aprovado automaticamente"
- "Pagamento confirmado"

## Passo 5 - Verificar fila DLQ (se houver falhas)

Se houver erros de consumo, verifique a DLQ do billing-events.

## Observacao

Se PaymentConfirmed nao aparecer:
- Verifique OutboxProcessor e credenciais AWS
- Verifique erros no MercadoPago mock
