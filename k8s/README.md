# Manifests Kubernetes - Oficina Cardozo API

Este diretório contém o esqueleto mínimo para rodar a API `OficinaCardozo.API` em um cluster Kubernetes (EKS).

## Arquivos

- `deployment.yaml`: Deployment da API (`oficina-cardozo-api`). O pipeline de CI/CD atualiza a imagem via `kubectl set image`.
- `service.yaml`: Service `ClusterIP` para expor a API internamente no cluster.

## Fluxo com o pipeline atual

1. O workflow em `.github/workflows/app-ci.yml`:
   - Builda e publica a imagem Docker `marciocardozodev/oficinacardozo-api:<tag>`.
   - Configura o `kubectl` usando o kubeconfig recebido por secret.
   - Faz `kubectl -n <namespace> set image deployment/oficina-cardozo-api api=marciocardozodev/oficinacardozo-api:<tag>`.
2. Antes do primeiro deploy automático, aplique os manifests uma vez no namespace desejado:

```bash
kubectl -n <namespace> apply -f k8s/deployment.yaml
kubectl -n <namespace> apply -f k8s/service.yaml
```

Depois disso, o pipeline apenas atualiza a imagem do Deployment existente.

> Observação: variáveis sensíveis (connection string do RDS, JWT secret, senha de e-mail) devem ser configuradas via `Secrets` e/ou `ConfigMaps` em manifests adicionais ou em um chart Helm, não estão incluídas neste esqueleto.
