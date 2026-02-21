# Configuração: Mercado Pago com GitHub Actions e Kubernetes

## 📋 Resumo

A integração do Mercado Pago é configurable via secrets do GitHub Actions e Kubernetes:

1. **GitHub Actions**: Armazena os tokens reais com segurança
2. **Pipeline (deploy.yaml)**: Injeta os secrets no Kubernetes
3. **Kubernetes Secret**: Injeta as variáveis de ambiente no container

---

## 🔐 Passo 1: Configurar Secrets no GitHub Actions

Acesse: **Settings > Secrets and variables > Actions**

Crie os seguintes secrets:

### Credenciais do Mercado Pago

| Nome do Secret | Valor | Exemplo |
|---|---|---|
| `MERCADOPAGO_ACCESS_TOKEN` | Seu token de teste ou produção | `APP_USR-xxxxxxxxxxxxxxxxxxxx` |
| `MERCADOPAGO_IS_SANDBOX` | `true` para testes, `false` para produção | `true` |
| `MERCADOPAGO_TEST_EMAIL` | Email para testes (sandbox) | `test@example.com` |
| `MERCADOPAGO_TEST_CARD_TOKEN` | Token de cartão de teste | `4111111111111111` |

### Credenciais do Docker (para fazer push da imagem)

| Nome do Secret | Valor |
|---|---|
| `DOCKER_USERNAME` | Seu username do Docker Hub |
| `DOCKER_PASSWORD` | Seu access token do Docker Hub |

### Credenciais do Kubernetes (para fazer deploy)

| Nome do Secret | Valor |
|---|---|
| `KUBECONFIG` | Seu kubeconfig em base64 |

**Como gerar KUBECONFIG em base64:**
```bash
cat ~/.kube/config | base64
```

---

## 🚀 Passo 2: Como funciona o Pipeline

Quando você faz `git push` para `develop` ou `main`:

1. **Build**: Compila o projeto .NET
2. **Docker**: Cria imagem e faz push para Docker Hub
3. **Deploy**: Cria Secret do Mercado Pago no Kubernetes
4. **Rollout**: Atualiza o deployment com a nova imagem

### Arquivo da Pipeline

- Localização: `.github/workflows/deploy.yaml`
- Triggers: Push em `develop` ou `main`

---

## 🔧 Passo 3: Secret do Kubernetes

O arquivo `deploy/k8s/mercadopago-secret.yaml` define a estrutura:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: billingservice-mercadopago
type: Opaque
stringData:
  MERCADOPAGO_ACCESS_TOKEN: "APP_USR-SEU_TOKEN_AQUI"
  MERCADOPAGO_IS_SANDBOX: "true"
  MERCADOPAGO_TEST_EMAIL: "test@example.com"
  MERCADOPAGO_TEST_CARD_TOKEN: "4111111111111111"
```

A pipeline substitui os valores pelos secrets do GitHub Actions automaticamente.

---

## 📊 Verificar a Configuração

### 1. Ver logs da pipeline
GitHub > Actions > Último workflow > Ver logs

### 2. Verificar Secret no Kubernetes
```bash
kubectl get secret billingservice-mercadopago -o yaml
```

### 3. Ver variáveis de ambiente no Pod
```bash
kubectl exec -it <pod-name> -- env | grep MERCADOPAGO
```

### 4. Ver qual serviço está sendo usado (mock ou real)
```bash
kubectl logs <pod-name> | grep "Mercado Pago"
```

---

## 🎯 Fluxo Completo

```
GitHub Secrets
    ↓
Pipeline (deploy.yaml)
    ↓
kubectl create secret (injeta valores reais)
    ↓
Kubernetes Secret (billingservice-mercadopago)
    ↓
Deployment (referencia o Secret)
    ↓
Container startup
    ↓
Program.cs lê Environment.GetEnvironmentVariable()
    ↓
MercadoPagoService (real) ou MercadoPagoMockService (mock)
```

---

## 🧪 Cenários

### Cenário 1: Testes com Mock
- `MERCADOPAGO_ACCESS_TOKEN`: vazio ou sem configurar
- `MERCADOPAGO_IS_SANDBOX`: `true`
- **Resultado**: Usa `MercadoPagoMockService`

### Cenário 2: Testes com Sandbox Real
- `MERCADOPAGO_ACCESS_TOKEN`: `APP_USR-sandbox-token-xxx`
- `MERCADOPAGO_IS_SANDBOX`: `true`
- **Resultado**: Usa `MercadoPagoService` (SDK real) com API de sandbox

### Cenário 3: Produção Real
- `MERCADOPAGO_ACCESS_TOKEN`: `APP_USR-production-token-xxx`
- `MERCADOPAGO_IS_SANDBOX`: `false`
- **Resultado**: Usa `MercadoPagoService` (SDK real) com API de produção

---

## ⚠️ Notas Importantes

1. **Nunca commite tokens no código**: Use apenas GitHub Secrets
2. **Diferentes tokens por ambiente**: Use branches diferentes (develop = sandbox, main = production)
3. **Validar em logs**: Sempre confira os logs para ver qual serviço está sendo usado
4. **Rotate tokens regularmente**: Por razões de segurança

---

## 📞 Troubleshooting

### Pipeline falha na etapa "Create Mercado Pago Secret"
- Verifique se os secrets do GitHub estão configurados
- Verifique a sintaxe do kubeconfig em base64

### Container não carrega as variáveis
- Verifique se o Secret foi criado: `kubectl get secret billingservice-mercadopago`
- Verifique se o Deployment referencia o Secret corretamente

### Usando Mock em vez de Real
- Confira se `MERCADOPAGO_ACCESS_TOKEN` está preenchido nos GitHub Secrets
- Confira se `MERCADOPAGO_IS_SANDBOX` tem o valor esperado

---

## 📚 Referências

- [GitHub Actions Secrets](https://docs.github.com/en/actions/security-guides/using-secrets-in-github-actions)
- [Kubernetes Secrets](https://kubernetes.io/docs/concepts/configuration/secret/)
- [Mercado Pago SDK .NET](https://github.com/mercadopago/sdk-dotnet)
