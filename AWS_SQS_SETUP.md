# AWS SQS Configuration para BillingService

## 🔧 Configuração AWS

### 1. Secrets do GitHub (required)

Adicione no repositório GitHub → Settings → Secrets and variables → Actions → New repository secret:

```
AWS_ACCESS_KEY_ID        → Sua chave de acesso AWS
AWS_SECRET_ACCESS_KEY    → Sua chave secreta AWS
DB_HOST                  → Host do RDS PostgreSQL (ex: billingservice-rds.xyz.sa-east-1.rds.amazonaws.com)
DB_USER                  → Usuário PostgreSQL (ex: postgres)
DB_PASSWORD              → Senha PostgreSQL
DB_NAME                  → Nome banco (ex: billingservice)
JWT_KEY                  → Chave secreta JWT (gerar: openssl rand -hex 32)
DOCKERHUB_USERNAME       → Seu usuário Docker Hub
DOCKERHUB_TOKEN          → Seu PAT do Docker Hub
```

### 2. Variáveis de Ambiente (automaticamente criadas pelo CI/CD)

O CI/CD **provisiona automaticamente**:

#### Via Terraform (SQS):
```
billing_queue_url        → URL da fila SQS principal
billing_queue_arn        → ARN da fila (para políticas)
billing_dlq_url          → URL da Dead Letter Queue
billing_service_role_arn → ARN da IAM Role para IRSA
```

#### Via Kubernetes (Secrets/ConfigMap):
```
billingservice-db-secret        → DB_HOST, DB_USER, DB_PASSWORD, DB_NAME
billingservice-secrets          → JWT_KEY
billingservice-sqs-secret       → SQS_QUEUE_URL
billingservice-config           → ASPNETCORE_ENVIRONMENT, AWS_REGION
```

## 📋 Fluxo de Deploy Completo

### Pré-requisitos:
1. ✅ AWS Account com EKS cluster (oficina-cardozo-eks)
2. ✅ Credenciais AWS (AccessKeyID + SecretAccessKey)
3. ✅ GitHub Secrets configurados (veja acima)
4. ✅ Docker Hub account (para push de imagens)

### Execução:
```
GitHub Push → refs/heads/master ou homolog
    ↓
CI/CD Triggers
    ├─ Build & Test (.NET)
    ├─ Push Docker Image
    ├─ Provision RDS PostgreSQL (Terraform)
    ├─ Provision SQS Queues (Terraform)
    ├─ Apply IRSA ServiceAccount
    ├─ Create Database (create-db-job)
    ├─ Deploy BillingService (K8s)
    └─ Verify Rollout
```

## 🔐 IRSA (IAM Roles for Service Accounts)

O CI/CD configura **IRSA automaticamente** para que o Pod acesse SQS sem credenciais hardcoded:

```yaml
# ServiceAccount no Kubernetes
apiVersion: v1
kind: ServiceAccount
metadata:
  name: billingservice
  annotations:
    eks.amazonaws.com/role-arn: arn:aws:iam::ACCOUNT_ID:role/billingservice-irsa-role
```

O Pod usa sua **IAM Role** automaticamente via Webhook do EKS.

### Permissões da IAM Role (criadas pelo Terraform):
```json
{
  "Effect": "Allow",
  "Action": [
    "sqs:ReceiveMessage",
    "sqs:DeleteMessage",
    "sqs:GetQueueAttributes",
    "sqs:ChangeMessageVisibility",
    "sqs:SendMessage"
  ],
  "Resource": [
    "arn:aws:sqs:sa-east-1:ACCOUNT_ID:billing-events",
    "arn:aws:sqs:sa-east-1:ACCOUNT_ID:billing-events-dlq"
  ]
}
```

## 🧪 Teste Local (sem AWS)

Use **LocalStack** para simular SQS localmente:

```bash
# 1. Iniciar LocalStack
docker run -p 4566:4566 localstack/localstack:latest

# 2. Criar fila
aws sqs create-queue \
  --queue-name billing-events \
  --endpoint-url http://localhost:4566

# 3. Executar BillingService
export SQS_QUEUE_URL=http://localhost:4566/000000000000/billing-events
export DB_HOST=localhost
export DB_USER=postgres
export DB_PASSWORD=postgres
export DB_NAME=billingservice
dotnet run

# 4. Enviar evento teste
aws sqs send-message \
  --queue-url http://localhost:4566/000000000000/billing-events \
  --message-body '{
    "correlationId": "550e8400-e29b-41d4-a716-446655440000",
    "causationId": "12345678-1234-1234-1234-123456789012",
    "timestamp": "2026-02-18T10:00:00Z",
    "payload": {
      "osId": "550e8400-e29b-41d4-a716-446655440000",
      "description": "Test service",
      "createdAt": "2026-02-18T10:00:00Z"
    }
  }' \
  --endpoint-url http://localhost:4566

# 5. Validar via GET REST
curl -X GET "http://localhost:5000/billing/budgets/550e8400-e29b-41d4-a716-446655440000"
```

## 📊 Monitoramento

### Verificar SQS no CloudWatch:
```bash
# Listar filas
aws sqs list-queues --region sa-east-1

# Ver metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/SQS \
  --metric-name ApproximateNumberOfMessagesVisible \
  --dimensions Name=QueueName,Value=billing-events \
  --start-time 2026-02-18T00:00:00Z \
  --end-time 2026-02-18T23:59:59Z \
  --period 300 \
  --statistics Average
```

### Logs do BillingService:
```bash
kubectl logs -f deployment/billingservice -n default
```

### Verificar IRSA:
```bash
# Token OIDC do Pod
kubectl exec -it deployment/billingservice -n default -- \
  cat /var/run/secrets/eks.amazonaws.com/serviceaccount/token

# Assumir Role (dentro do Pod)
aws sts assume-role-with-web-identity \
  --role-arn arn:aws:iam::ACCOUNT_ID:role/billingservice-irsa-role \
  --role-session-name billing-session \
  --web-identity-token <TOKEN>
```

## 🛠️ Troubleshooting

### Erro: Pod não consegue acessar SQS
```bash
# 1. Verificar ServiceAccount
kubectl get sa billingservice -o yaml

# 2. Verificar IRSA annotation
kubectl describe sa billingservice

# 3. Verificar se o OIDC Provider existe
aws iam list-open-id-connect-providers

# 4. Testar credenciais no Pod
kubectl exec -it deployment/billingservice -- aws sqs list-queues
```

### Erro: SQS_QUEUE_URL não encontrado
```bash
# Verificar secret
kubectl get secret billingservice-sqs-secret -o yaml

# SQS_QUEUE_URL deve estar em: .data.SQS_QUEUE_URL (base64)
```

### Erro: Mensagens vão para DLQ
```bash
# Verificar DLQ
aws sqs receive-message \
  --queue-url https://sqs.sa-east-1.amazonaws.com/ACCOUNT_ID/billing-events-dlq \
  --region sa-east-1

# Logs do Pod
kubectl logs deployment/billingservice -n default | grep -i error
```

## 📚 Referências

- [AWS IRSA Documentation](https://docs.aws.amazon.com/eks/latest/userguide/iam-roles-for-service-accounts.html)
- [AWS SQS Best Practices](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/)
- [LocalStack Documentation](https://docs.localstack.cloud/)
- [GitHub Actions AWS Credentials](https://github.com/aws-actions/configure-aws-credentials)
