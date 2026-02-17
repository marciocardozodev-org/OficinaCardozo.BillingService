# Exemplos de Eventos

## BudgetGenerated
```
{
  "CorrelationId": "...",
  "CausationId": "...",
  "Timestamp": "2026-02-17T12:00:00Z",
  "Payload": {
    "OsId": "...",
    "BudgetId": "...",
    "Amount": 123.45,
    "Status": "Generated"
  }
}
```

## BudgetApproved
```
{
  "CorrelationId": "...",
  "CausationId": "...",
  "Timestamp": "2026-02-17T12:01:00Z",
  "Payload": {
    "OsId": "...",
    "BudgetId": "...",
    "Status": "Approved"
  }
}
```

## PaymentConfirmed
```
{
  "CorrelationId": "...",
  "CausationId": "...",
  "Timestamp": "2026-02-17T12:02:00Z",
  "Payload": {
    "OsId": "...",
    "PaymentId": "...",
    "Status": "Confirmed",
    "Amount": 123.45,
    "ProviderPaymentId": "..."
  }
}
```
