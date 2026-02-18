# ✅ Checklist de Deploy BillingService

## Fase 1: GitHub Secrets (Pré-requisito)

- [ ] Obter credenciais AWS (Access Key ID + Secret Access Key)
- [ ] Gerar JWT_KEY: `openssl rand -hex 32`
- [ ] Determinar valores de RDS:
  - [ ] DB_HOST (endpoint RDS)
  - [ ] DB_USER (ex: postgres)
  - [ ] DB_PASSWORD (senha)
  - [ ] DB_NAME (ex: billingservice)
- [ ] Obter credenciais Docker Hub:
  - [ ] DOCKERHUB_USERNAME
  - [ ] DOCKERHUB_TOKEN
- [ ] Adicionar no GitHub → Settings → Secrets and variables → Actions:
  ```
  AWS_ACCESS_KEY_ID
  AWS_SECRET_ACCESS_KEY
  DB_HOST
  DB_USER
  DB_PASSWORD
  DB_NAME
  JWT_KEY
  DOCKERHUB_USERNAME
  DOCKERHUB_TOKEN
  ```

## Fase 2: Validação Local (Sem AWS)

- [ ] Build local bem-sucedido: `dotnet build`
  - Status Atual: ✅ **BUILD SUCESSO** (16 warnings, 0 errors)
- [ ] Testes locais com LocalStack (preparação)
  ```bash
  docker run -p 4566:4566 localstack/localstack:latest &
  aws sqs create-queue --queue-name billing-events --endpoint-url http://localhost:4566
  ```

## Fase 3: Arquivos Kubernetes

- [ ] ConfigMap público criado: `deploy/k8s/aws-messaging-config.yaml`
  - Status Atual: ✅ Arquivo criado com AWS_REGION, SQS URLs, SNS ARNs
- [ ] Deployment atualizado: `deploy/k8s/deployment.yaml`
  - Status Atual: ✅ Usa aws-messaging-config + aws-messaging-secrets
- [ ] Job de DB criado: `deploy/k8s/create-db-job.yaml`
  - Status Atual: ✅ Já existe

## Fase 4: CI/CD Pipeline

- [ ] GitHub Actions pipeline configurada: `.github/workflows/ci-cd-billingservice.yml`
  - Status Atual: ✅ Pipeline criada com:
    - ✅ Build .NET
    - ✅ Docker push
    - ✅ Terraform RDS
    - ✅ Aplicar aws-messaging-config.yaml
    - ✅ Criar aws-messaging-secrets
    - ✅ Deploy BillingService
- [ ] EKS cluster configurado:
  - [ ] Cluster nome: `oficina-cardozo-eks`
  - [ ] Região: `sa-east-1`
  - [ ] Conta AWS: `953082827427`
- [ ] IAM User com permissões:
  - [ ] SQS (send, receive, delete)
  - [ ] SNS (publish)
  - [ ] RDS (connect)
  - [ ] EKS (deploy)

## Fase 5: Deploy

### 5.1 - Via GitHub Actions (Recomendado)
- [ ] Fazer commit de todas as mudanças
- [ ] Push para `master` ou `homolog` branch
  ```bash
  git add .
  git commit -m "feat: BillingService com ConfigMap+Secret (padrão OSService)"
  git push origin master
  ```
- [ ] Monitorar GitHub Actions:
  - [ ] Build & Test: ✅ Pass
  - [ ] Docker Push: ✅ Pass
  - [ ] Terraform RDS: ✅ Pass
  - [ ] Kubernetes Deploy: ✅ Pass
- [ ] Validar recursos K8s:
  ```bash
  kubectl get configmap aws-messaging-config -o yaml
  kubectl get secret aws-messaging-secrets -o yaml
  kubectl get deployment billingservice
  kubectl logs -f deployment/billingservice
  ```

### 5.2 - Manual (Se necessário)
```bash
# 1. Build local
dotnet build

# 2. Aplicar ConfigMap
kubectl apply -f deploy/k8s/aws-messaging-config.yaml

# 3. Criar aws-messaging-secrets
kubectl create secret generic aws-messaging-secrets \
  --from-literal=AWS_ACCESS_KEY_ID="seu_access_key" \
  --from-literal=AWS_SECRET_ACCESS_KEY="seu_secret_key" \
  --dry-run=client -o yaml | kubectl apply -f -

# 4. Deploy
kubectl apply -f deploy/k8s/deployment.yaml

# 5. Monitorar
kubectl logs -f deployment/billingservice
```

## Fase 6: Validação do Deploy

- [ ] Pod iniciando: `kubectl logs deployment/billingservice`
  - Procurar por: ✅ "BillingService running"
  - ❌ Evitar: "AWS_ACCESS_KEY_ID not found" ou similar
- [ ] ConfigMap injetado:
  ```bash
  kubectl exec -it <pod_name> -- printenv | grep AWS_REGION
  # Esperado: AWS_REGION=sa-east-1
  ```
- [ ] Banco de dados conectando:
  ```bash
  kubectl logs deployment/billingservice | grep -i "database\|postgres\|connection"
  ```
- [ ] SQS consumindo eventos:
  ```bash
  kubectl logs deployment/billingservice | grep -i "sqs\|queue\|message"
  ```

## Fase 7: Teste Funcional

### 7.1 - Teste Orçamento
```bash
# Port-forward para acessar localmente
kubectl port-forward svc/billingservice 5000:5000 &

# 1. Criar orçamento
curl -X POST http://localhost:5000/api/billing/orcamento \
  -H "Content-Type: application/json" \
  -d '{
    "osId": "550e8400-e29b-41d4-a716-446655440000",
    "cliente": "Cliente Teste",
    "valor": 1000.00,
    "descricao": "Serviço de teste"
  }'
# Esperado: 200 OK com ID do orçamento

# 2. Consultar orçamento
curl -X GET http://localhost:5000/billing/budgets/550e8400-e29b-41d4-a716-446655440000
# Esperado: 200 OK com dados do orçamento
```

### 7.2 - Validar Outbox
```bash
# Conectar ao banco de dados
kubectl exec -it <pod_name> -- psql \
  -h $DB_HOST \
  -U $DB_USER \
  -d billingservice

# Query Outbox (eventos pendentes)
SELECT * FROM outbox_message WHERE published = false;

# Query Inbox (eventos processados)
SELECT * FROM inbox_message;
```

### 7.3 - Validar SQS
```bash
aws sqs receive-message \
  --queue-url https://sqs.sa-east-1.amazonaws.com/953082827427/billing-events \
  --region sa-east-1 \
  --profile seu_profile

# Esperado: Mensagens de OsCreated sendo processadas
```

## Fase 8: Monitoramento em Produção

- [ ] Pod estar healthy:
  ```bash
  kubectl get pods -o wide
  # Status: Running, Ready: 1/1
  ```
- [ ] Eventos sendo processados:
  ```bash
  kubectl logs -f deployment/billingservice | tail -50
  # Procurar por: "Publishing event", "Received message"
  ```
- [ ] Alertas configurados (CloudWatch)
  - [ ] SQS DLQ messages (enviar para DLQ = erro)
  - [ ] Pod restart count > 0
  - [ ] Database connection failures

## Status Atual (Antes dos Secrets)

| Componente | Status | Detalhes |
|-----------|--------|----------|
| **Build** | ✅ Sucesso | 0 errors, 16 warnings (nullability) |
| **ConfigMap** | ✅ Criado | aws-messaging-config.yaml |
| **Deployment** | ✅ Atualizado | Referencia ConfigMap + Secret |
| **Program.cs** | ✅ Configurado | Lê credenciais do environment |
| **CI/CD** | ✅ Pronto | Falta push + GitHub Secrets |
| **GitHub Secrets** | ⏳ Pendente | Próximo passo |

## 🎯 Próximos Passos Imediatos

1. **Configure GitHub Secrets** (Fase 1) - 5 minutos
2. **Push para master** - trigger automático
3. **Acompanhe GitHub Actions** - monitor logs
4. **Valide ConfigMap/Secrets** - kubectl get
5. **Teste endpoint** - curl /api/billing/orcamento

---

**Documentação Relacionada:**
- [KUBERNETES_CONFIG_STRATEGY.md](./KUBERNETES_CONFIG_STRATEGY.md) - Explicação detalhada
- [IMPLEMENTATION_COMPLETE.md](./IMPLEMENTATION_COMPLETE.md) - Implementação .NET
- [AWS_SQS_SETUP.md](./AWS_SQS_SETUP.md) - Configuração SQS
