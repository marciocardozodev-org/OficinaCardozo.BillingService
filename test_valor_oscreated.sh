#!/bin/bash
# Teste E2E: Valor do evento OsCreated propagado para orçamento/pagamento
set -e

echo "🧪 Teste: Valor do OsCreated propagado corretamente"
echo "================================================================"

# Obter token JWT
JWT_KEY=$(cat /tmp/jwt_key.txt)
TOKEN=$(python3 << EOF
import jwt
from datetime import datetime, timedelta, timezone
now = datetime.now(timezone.utc)
token = jwt.encode({
    "sub": "admin",
    "iat": int(now.timestamp()),
    "exp": int((now + timedelta(hours=24)).timestamp())
}, "$JWT_KEY", algorithm="HS256")
print(token)
EOF
)

echo ""
echo "📌 CENÁRIO A: OsCreated com Valor=0.01 (IDEAL)"
echo "----------------------------------------------------------------"

# Simular evento OsCreated com Valor=0.01
python3 << 'EOF'
import boto3
import json
import uuid
from datetime import datetime

sqs = boto3.client('sqs', region_name='sa-east-1')
queue_url = "https://sqs.sa-east-1.amazonaws.com/953082827427/billing-events"

os_id = str(uuid.uuid4())
correlation_id = str(uuid.uuid4())

# Evento com Valor especificado
event_payload = {
    "OsId": os_id,
    "Description": "OS de teste com valor 0.01",
    "CreatedAt": datetime.utcnow().isoformat() + "Z",
    "Valor": 0.01
}

envelope = {
    "EventType": "OsCreated",
    "CorrelationId": correlation_id,
    "CausationId": str(uuid.uuid4()),
    "Payload": event_payload,
    "Timestamp": datetime.utcnow().isoformat() + "Z"
}

# Enviar para SQS
sqs.send_message(
    QueueUrl=queue_url,
    MessageBody=json.dumps(envelope)
)

print(f"✅ Evento enviado: OsId={os_id}, Valor=0.01")
print(f"   CorrelationId: {correlation_id}")

# Salvar para verificação
with open('/tmp/test_cenario_a.txt', 'w') as f:
    f.write(f"{os_id}|{correlation_id}")
EOF

echo ""
echo "⏳ Aguardando processamento (10s)..."
sleep 10

# Verificar se orçamento foi criado com valor correto
OS_CORRELATION=$(cat /tmp/test_cenario_a.txt)
OS_ID=$(echo $OS_CORRELATION | cut -d'|' -f1)
CORRELATION_ID=$(echo $OS_CORRELATION | cut -d'|' -f2)

echo ""
echo "🔍 Verificando orçamento criado..."

# Buscar no outbox
python3 << EOF
import requests

headers = {"Authorization": f"Bearer $TOKEN"}
resp = requests.get("http://localhost:8080/api/billing/outbox", headers=headers, timeout=10)

if resp.status_code == 200:
    events = resp.json().get('events', [])
    budget_events = [e for e in events if e['eventType'] == 'BudgetGenerated' 
                     and e.get('CorrelationId') == "$CORRELATION_ID"]
    
    if budget_events:
        import json
        payload = json.loads(budget_events[0]['Payload'])
        amount = payload.get('Amount')
        
        if amount == 0.01:
            print(f"✅ SUCESSO! BudgetGenerated.Amount = {amount} (esperado: 0.01)")
        else:
            print(f"❌ FALHA! BudgetGenerated.Amount = {amount} (esperado: 0.01)")
            exit(1)
    else:
        print(f"⚠️  BudgetGenerated não encontrado para CorrelationId={CORRELATION_ID}")
        exit(1)
else:
    print(f"❌ Erro ao buscar outbox: {resp.status_code}")
    exit(1)
EOF

echo ""
echo "----------------------------------------------------------------"
echo "📌 CENÁRIO B: OsCreated sem campo Valor (FALLBACK)"
echo "----------------------------------------------------------------"

python3 << 'EOF'
import boto3
import json
import uuid
from datetime import datetime

sqs = boto3.client('sqs', region_name='sa-east-1')
queue_url = "https://sqs.sa-east-1.amazonaws.com/953082827427/billing-events"

os_id = str(uuid.uuid4())
correlation_id = str(uuid.uuid4())

# Evento SEM campo Valor (compatibilidade retroativa)
event_payload = {
    "OsId": os_id,
    "Description": "OS de teste SEM valor",
    "CreatedAt": datetime.utcnow().isoformat() + "Z"
}

envelope = {
    "EventType": "OsCreated",
    "CorrelationId": correlation_id,
    "CausationId": str(uuid.uuid4()),
    "Payload": event_payload,
    "Timestamp": datetime.utcnow().isoformat() + "Z"
}

sqs.send_message(
    QueueUrl=queue_url,
    MessageBody=json.dumps(envelope)
)

print(f"✅ Evento enviado: OsId={os_id}, Valor=NULL")
print(f"   CorrelationId: {correlation_id}")

with open('/tmp/test_cenario_b.txt', 'w') as f:
    f.write(f"{os_id}|{correlation_id}")
EOF

echo ""
echo "⏳ Aguardando processamento (10s)..."
sleep 10

OS_CORRELATION=$(cat /tmp/test_cenario_b.txt)
CORRELATION_ID=$(echo $OS_CORRELATION | cut -d'|' -f2)

echo ""
echo "🔍 Verificando fallback (deve ser 100.00)..."

python3 << EOF
import requests

headers = {"Authorization": f"Bearer $TOKEN"}
resp = requests.get("http://localhost:8080/api/billing/outbox", headers=headers, timeout=10)

if resp.status_code == 200:
    events = resp.json().get('events', [])
    budget_events = [e for e in events if e['eventType'] == 'BudgetGenerated' 
                     and e.get('CorrelationId') == "$CORRELATION_ID"]
    
    if budget_events:
        import json
        payload = json.loads(budget_events[0]['Payload'])
        amount = payload.get('Amount')
        
        if amount == 100.00:
            print(f"✅ SUCESSO! Fallback aplicado: Amount = {amount}")
        else:
            print(f"❌ FALHA! Amount = {amount} (esperado: 100.00)")
            exit(1)
    else:
        print(f"⚠️  BudgetGenerated não encontrado")
        exit(1)
EOF

echo ""
echo "----------------------------------------------------------------"
echo "📌 CENÁRIO C: OsCreated com Valor <= 0 (FALLBACK)"
echo "----------------------------------------------------------------"

python3 << 'EOF'
import boto3
import json
import uuid
from datetime import datetime

sqs = boto3.client('sqs', region_name='sa-east-1')
queue_url = "https://sqs.sa-east-1.amazonaws.com/953082827427/billing-events"

os_id = str(uuid.uuid4())
correlation_id = str(uuid.uuid4())

# Evento com Valor inválido (negativo)
event_payload = {
    "OsId": os_id,
    "Description": "OS de teste com valor negativo",
    "CreatedAt": datetime.utcnow().isoformat() + "Z",
    "Valor": -10.00
}

envelope = {
    "EventType": "OsCreated",
    "CorrelationId": correlation_id,
    "CausationId": str(uuid.uuid4()),
    "Payload": event_payload,
    "Timestamp": datetime.utcnow().isoformat() + "Z"
}

sqs.send_message(
    QueueUrl=queue_url,
    MessageBody=json.dumps(envelope)
)

print(f"✅ Evento enviado: OsId={os_id}, Valor=-10.00")
print(f"   CorrelationId: {correlation_id}")

with open('/tmp/test_cenario_c.txt', 'w') as f:
    f.write(f"{os_id}|{correlation_id}")
EOF

echo ""
echo "⏳ Aguardando processamento (10s)..."
sleep 10

OS_CORRELATION=$(cat /tmp/test_cenario_c.txt)
CORRELATION_ID=$(echo $OS_CORRELATION | cut -d'|' -f2)

echo ""
echo "🔍 Verificando fallback (deve ser 100.00)..."

python3 << EOF
import requests

headers = {"Authorization": f"Bearer $TOKEN"}
resp = requests.get("http://localhost:8080/api/billing/outbox", headers=headers, timeout=10)

if resp.status_code == 200:
    events = resp.json().get('events', [])
    budget_events = [e for e in events if e['eventType'] == 'BudgetGenerated' 
                     and e.get('CorrelationId') == "$CORRELATION_ID"]
    
    if budget_events:
        import json
        payload = json.loads(budget_events[0]['Payload'])
        amount = payload.get('Amount')
        
        if amount == 100.00:
            print(f"✅ SUCESSO! Fallback aplicado: Amount = {amount}")
        else:
            print(f"❌ FALHA! Amount = {amount} (esperado: 100.00)")
            exit(1)
    else:
        print(f"⚠️  BudgetGenerated não encontrado")
        exit(1)
EOF

echo ""
echo "================================================================"
echo "✅ TODOS OS CENÁRIOS PASSARAM!"
echo "================================================================"
