#!/bin/bash
# Script para instalar CloudWatch Container Insights com FluentBit no EKS
# Isso permite que os logs dos pods sejam automaticamente enviados para CloudWatch

set -e

CLUSTER_NAME="${CLUSTER_NAME:-oficinacardozo-cluster}"
AWS_REGION="${AWS_REGION:-sa-east-1}"

echo "🚀 Instalando CloudWatch Container Insights no cluster: $CLUSTER_NAME"
echo "   Região: $AWS_REGION"

# 1. Criar namespace amazon-cloudwatch se não existir
echo ""
echo "📦 Criando namespace amazon-cloudwatch..."
kubectl create namespace amazon-cloudwatch --dry-run=client -o yaml | kubectl apply -f -

# 2. Criar ConfigMap do FluentBit
echo ""
echo "📝 Criando ConfigMap do FluentBit..."
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: fluent-bit-config
  namespace: amazon-cloudwatch
  labels:
    k8s-app: fluent-bit
data:
  fluent-bit.conf: |
    [SERVICE]
        Flush                     5
        Log_Level                 info
        Daemon                    off
        Parsers_File              parsers.conf
        HTTP_Server               On
        HTTP_Listen               0.0.0.0
        HTTP_Port                 2020

    [INPUT]
        Name                tail
        Path                /var/log/containers/*.log
        Parser              docker
        Tag                 kube.*
        Refresh_Interval    5
        Mem_Buf_Limit       50MB
        Skip_Long_Lines     On

    [FILTER]
        Name                kubernetes
        Match               kube.*
        Kube_URL            https://kubernetes.default.svc.cluster.local:443
        Merge_Log           On
        Keep_Log            Off
        K8S-Logging.Parser  On
        K8S-Logging.Exclude On

    [OUTPUT]
        Name                cloudwatch
        Match               kube.*
        region              ${AWS_REGION}
        log_group_name      /eks/prod/\${kubernetes['namespace_name']}/\${kubernetes['pod_name']}
        log_stream_prefix   \${kubernetes['pod_name']}-
        auto_create_group   true
        log_retention_days  30

  parsers.conf: |
    [PARSER]
        Name                docker
        Format              json
        Time_Key            time
        Time_Format         %Y-%m-%dT%H:%M:%S.%LZ
        Time_Keep           On

    [PARSER]
        Name                syslog
        Format              regex
        Regex               ^\<(?<pri>[0-9]+)\>(?<time>[^ ]* {1,2}[^ ]* [^ ]*) (?<host>[^ ]*) (?<ident>[a-zA-Z0-9_\/\.\-]*)(?:\[(?<pid>[0-9]+)\])?(?:[^\:]*\:)? *(?<message>.*)$
        Time_Key            time
        Time_Format         %b %d %H:%M:%S
EOF

# 3. Criar ServiceAccount com permissões IAM
echo ""
echo "👤 Criando ServiceAccount..."
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ServiceAccount
metadata:
  name: fluent-bit
  namespace: amazon-cloudwatch
EOF

# 4. Criar ClusterRole e ClusterRoleBinding
echo ""
echo "🔐 Criando ClusterRole e ClusterRoleBinding..."
cat <<EOF | kubectl apply -f -
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: fluent-bit-role
rules:
  - apiGroups: [""]
    resources:
      - namespaces
      - pods
      - pods/logs
    verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: fluent-bit-role-binding
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: fluent-bit-role
subjects:
  - kind: ServiceAccount
    name: fluent-bit
    namespace: amazon-cloudwatch
EOF

# 5. Deploy FluentBit DaemonSet
echo ""
echo "🚀 Deployando FluentBit DaemonSet..."
cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: fluent-bit
  namespace: amazon-cloudwatch
  labels:
    k8s-app: fluent-bit
spec:
  selector:
    matchLabels:
      k8s-app: fluent-bit
  template:
    metadata:
      labels:
        k8s-app: fluent-bit
    spec:
      serviceAccountName: fluent-bit
      containers:
      - name: fluent-bit
        image: amazon/aws-for-fluent-bit:latest
        imagePullPolicy: Always
        env:
        - name: AWS_REGION
          value: "${AWS_REGION}"
        - name: CLUSTER_NAME
          value: "${CLUSTER_NAME}"
        - name: HOST_NAME
          valueFrom:
            fieldRef:
              fieldPath: spec.nodeName
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        resources:
          limits:
            memory: 200Mi
          requests:
            cpu: 100m
            memory: 100Mi
        volumeMounts:
        - name: varlog
          mountPath: /var/log
          readOnly: true
        - name: varlibdockercontainers
          mountPath: /var/lib/docker/containers
          readOnly: true
        - name: fluent-bit-config
          mountPath: /fluent-bit/etc/
      terminationGracePeriodSeconds: 10
      volumes:
      - name: varlog
        hostPath:
          path: /var/log
      - name: varlibdockercontainers
        hostPath:
          path: /var/lib/docker/containers
      - name: fluent-bit-config
        configMap:
          name: fluent-bit-config
EOF

# 6. Verificar status
echo ""
echo "✅ FluentBit instalado!"
echo ""
echo "📊 Verificando status dos pods FluentBit:"
kubectl get pods -n amazon-cloudwatch -l k8s-app=fluent-bit

echo ""
echo "✅ CONCLUÍDO"
echo ""
echo "ℹ️  Próximos passos:"
echo "   1. Aguarde ~2 minutos para os pods FluentBit ficarem prontos"
echo "   2. Verifique logs: kubectl logs -n amazon-cloudwatch -l k8s-app=fluent-bit"
echo "   3. No CloudWatch, procure por: /eks/prod/default/billingservice-*"
echo ""
echo "⚠️  IMPORTANTE: As credenciais AWS devem estar configuradas nos nodes do EKS"
echo "   (via IAM Role for Service Accounts ou Instance Profile)"
