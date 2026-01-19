# Datadog - Integração com API Oficina Cardozo

## Variáveis de ambiente necessárias
- DATADOG_API_KEY (Secret)
- DATADOG_METRICS_URL (ConfigMap, default: https://api.datadoghq.com/api/v1/series)
- DATADOG_HOST (ConfigMap, default: oficina-cardozo-api)
- POD_NAME (injetada automaticamente pelo deployment)

## Exemplo de uso no deployment.yaml
```yaml
      env:
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
```

## Exemplo de Secret (NUNCA versionar secrets reais)
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: datadog-secret
  labels:
    app: oficina-cardozo-api
type: Opaque
data:
  DATADOG_API_KEY: <INSIRA_AQUI_BASE64_DA_API_KEY>
```

## Exemplo de ConfigMap
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: datadog-config
  labels:
    app: oficina-cardozo-api
data:
  DATADOG_METRICS_URL: "https://api.datadoghq.com/api/v1/series"
  DATADOG_HOST: "oficina-cardozo-api"
```

## Checklist para integração
- [ ] Build/push da imagem da API no registry
- [ ] Criar Secret datadog-secret com a API key
- [ ] Criar ConfigMap datadog-config (se quiser customizar host/url)
- [ ] Garantir env POD_NAME no deployment
- [ ] Aplicar deployment.yaml com envFrom para secrets/configmaps
- [ ] Validar métricas no Datadog (Metrics Explorer)

---
Dúvidas? Consulte o time de DevOps ou o README do repositório App.
