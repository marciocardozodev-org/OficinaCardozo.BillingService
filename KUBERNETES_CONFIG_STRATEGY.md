# BillingService - Configuração AWS & Kubernetes (Estratégia OSService)

## ✅ Configuração Adotada

BillingService agora usa a **mesma estratégia do OSService**:
- ConfigMap público: `aws-messaging-config` (URLs, ARNs, região)
- Secret privado: `aws-messaging-secrets` (credenciais AWS)
- Sem IRSA (mais simples, credenciais diretas)

## 📋 GitHub Secrets Necessários

Configure no repositório GitHub → Settings → Secrets and variables → Actions:

```
AWS_ACCESS_KEY_ID              (sua access key AWS)
AWS_SECRET_ACCESS_KEY          (sua secret key AWS)
DB_HOST                        (host RDS, ex: billingservice-rds.xxx.sa-east-1.rds.amazonaws.com)
DB_USER                        (usuário PostgreSQL, ex: postgres)
DB_PASSWORD                    (senha PostgreSQL)
DB_NAME                        (nome banco, ex: billingservice)
JWT_KEY                        (chave JWT - gerar com: openssl rand -hex 32)
DOCKERHUB_USERNAME             (seu usuário Docker)
DOCKERHUB_TOKEN                (seu token Docker)
```

## 📂 Arquivos de Configuração Kubernetes

### ConfigMap Público (aws-messaging-config.yaml)
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: aws-messaging-config
  namespace: default
data:
  AWS_REGION: sa-east-1
  AWS_SQS_QUEUE_BILLING: https://sqs.sa-east-1.amazonaws.com/953082827427/billing-events
  AWS_SQS_QUEUE_DLQ_BILLING: https://sqs.sa-east-1.amazonaws.com/953082827427/billing-events-dlq
  AWS_SNS_TOPIC_BUDGETGENERATED: arn:aws:sns:sa-east-1:953082827427:budget-generated
  AWS_SNS_TOPIC_BUDGETAPPROVED: arn:aws:sns:sa-east-1:953082827427:budget-approved
  AWS_SNS_TOPIC_PAYMENTCONFIRMED: arn:aws:sns:sa-east-1:953082827427:payment-confirmed
  AWS_SNS_TOPIC_PAYMENTFAILED: arn:aws:sns:sa-east-1:953082827427:payment-failed
  AWS_SNS_TOPIC_PAYMENTREVERSED: arn:aws:sns:sa-east-1:953082827427:payment-reversed
```

### Secret Privado (criado automaticamente pelo CI/CD)
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: aws-messaging-secrets
  namespace: default
type: Opaque
data:
  AWS_ACCESS_KEY_ID: <base64_encoded_access_key>
  AWS_SECRET_ACCESS_KEY: <base64_encoded_secret_key>
```

### Deployment (deploy/k8s/deployment.yaml)
Usa referências aos Secrets e ConfigMaps:
```yaml
env:
- name: AWS_ACCESS_KEY_ID
  valueFrom:
    secretKeyRef:
      name: aws-messaging-secrets
      key: AWS_ACCESS_KEY_ID
- name: AWS_SECRET_ACCESS_KEY
  valueFrom:
    secretKeyRef:
      name: aws-messaging-secrets
      key: AWS_SECRET_ACCESS_KEY
- name: AWS_REGION
  valueFrom:
    configMapKeyRef:
      name: aws-messaging-config
      key: AWS_REGION
- name: AWS_SQS_QUEUE_BILLING
  valueFrom:
    configMapKeyRef:
      name: aws-messaging-config
      key: AWS_SQS_QUEUE_BILLING
# ... mais variáveis
```

## 🔧 Configuração do BillingService (.NET)

**Program.cs** lê credenciais do environment:
```csharp
var awsAccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
var awsSecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? "sa-east-1";
var sqsQueueUrl = Environment.GetEnvironmentVariable("AWS_SQS_QUEUE_BILLING") ?? "...";

// Configurar SQS com credenciais diretas
var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey);
var sqsConfig = new AmazonSQSConfig { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion) };
builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(awsCredentials, sqsConfig));
```

## 🚀 Fluxo de Deploy Automático (CI/CD)

1. **Build & Test** (.NET)
2. **Push Docker** (se master/homolog)
3. **Provision RDS** (Terraform)
4. **Create Kubernetes Resources**:
   - ✅ Certificado global-bundle.pem (ConfigMap)
   - ✅ Database secret (DB_HOST, DB_USER, DB_PASSWORD, DB_NAME)
   - ✅ Application config (ASPNETCORE_ENVIRONMENT)
   - ✅ Application secrets (JWT_KEY)
   - ✅ AWS messaging config (ConfigMap público)
   - ✅ AWS messaging secrets (credenciais AWS)
   - ✅ Database setup job (create-db-job)
5. **Deploy BillingService** (Deployment)

## 🧪 Teste Local (sem AWS)

Usar **LocalStack** para simular SQS:
```bash
# 1. Iniciar LocalStack
docker run -p 4566:4566 localstack/localstack:latest

# 2. Criar fila
aws sqs create-queue --queue-name billing-events --endpoint-url http://localhost:4566

# 3. Variáveis de ambiente locais
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_REGION=sa-east-1
export AWS_SQS_QUEUE_BILLING=http://localhost:4566/000000000000/billing-events
export DB_HOST=localhost
export DB_USER=postgres
export DB_PASSWORD=postgres
export DB_NAME=billingservice

# 4. Executar
dotnet run

# 5. Testar endpoint
curl -X GET "http://localhost:5000/billing/budgets/550e8400-e29b-41d4-a716-446655440000"
```

## 📊 Comparação com OSService

| Aspecto | OSService | BillingService |
|---------|-----------|-----------------|
| **Abordagem AWS** | Credenciais diretas (Access Key + Secret) | ✅ Credenciais diretas (mesma estratégia) |
| **ConfigMap** | `aws-messaging-config` (SNS, SQS URLs) | ✅ `aws-messaging-config` (SNS, SQS URLs) |
| **Secret** | `aws-messaging-secrets` | ✅ `aws-messaging-secrets` |
| **IRSA** | ❌ Não usa | ❌ Não usa (mantém consistência) |
| **ServiceAccount** | Padrão (sem anotações) | ✅ Padrão (sem anotações) |
| **Deployment** | Referencia ConfigMap + Secret | ✅ Referencia ConfigMap + Secret |

## ✅ Vantagens da Abordagem Adotada

1. **Consistência**: Mesmo padrão do OSService
2. **Simplicidade**: Sem complexidade de IRSA/OIDC
3. **Compatibilidade**: Funciona em qualquer cluster EKS
4. **Manutenção**: Fácil de atualizar ARNs/URLs no ConfigMap
5. **Segurança**: Credenciais em Secret, URLs em ConfigMap

## 🛠️ Próximas Ações

1. Configure os GitHub Secrets (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, etc.)
2. Faça push para `master` ou `homolog`
3. CI/CD automaticamente:
   - Criar `aws-messaging-secrets` usando credenciais do GitHub
   - Aplicar `aws-messaging-config.yaml`
   - Deploy BillingService com acesso a SQS
4. Validar via:
   ```bash
   kubectl logs -f deployment/billingservice
   # Deve ver eventos sendo consumidos de SQS
   ```

## 📚 Referências

- [Deploy similar ao OSService](../docs/osservice-deployment.md)
- [AWS SQS Documentation](https://docs.aws.amazon.com/sqs/)
- [Kubernetes ConfigMap & Secrets](https://kubernetes.io/docs/concepts/configuration/)
