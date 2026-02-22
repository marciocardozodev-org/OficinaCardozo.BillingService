#!/bin/bash

set -e

echo ""
echo "================================"
echo "🔷 BillingService Observabilidade - Passos de Validação"
echo "================================"
echo ""

# 1. Validar Build
echo "[1/5] Validando Build..."
cd /workspaces/OficinaCardozo.BillingService
dotnet build OFICINACARDOZO.BILLINGSERVICE.csproj --configuration Release > /dev/null 2>&1
echo "✅ Build realizado com sucesso"
echo ""

# 2. Verificar Pacotes Serilog
echo "[2/5] Verificando pacotes Serilog..."
grep -q "Serilog.AspNetCore" OFICINACARDOZO.BILLINGSERVICE.csproj && echo "  ✅ Serilog.AspNetCore instalado"
grep -q "Serilog.Sinks.Console" OFICINACARDOZO.BILLINGSERVICE.csproj && echo "  ✅ Serilog.Sinks.Console instalado"
grep -q "Serilog.Enrichers.Environment" OFICINACARDOZO.BILLINGSERVICE.csproj && echo "  ✅ Serilog.Enrichers.Environment instalado"
grep -q "Serilog.Enrichers.Thread" OFICINACARDOZO.BILLINGSERVICE.csproj && echo "  ✅ Serilog.Enrichers.Thread instalado"
echo ""

# 3. Verificar Middleware CorrelationId
echo "[3/5] Verificando Middleware CorrelationId..."
test -f "src/API/CorrelationIdMiddleware.cs" && echo "  ✅ Arquivo CorrelationIdMiddleware.cs existe"
grep -q "LogContext.PushProperty" src/API/CorrelationIdMiddleware.cs && echo "  ✅ LogContext.PushProperty implementado"
grep -q "GetCorrelationId" src/API/CorrelationIdMiddleware.cs && echo "  ✅ Helper GetCorrelationId disponível"
echo ""

# 4. Verificar Logs de Negócio
echo "[4/5] Verificando Logs de Negócio..."
grep -q "gerou evento" src/Messaging/OutboxProcessor.cs && echo "  ✅ OutboxProcessor registra publicação de eventos"
grep -q "consumiu evento" src/Handlers/OsCreatedHandler.cs && echo "  ✅ OsCreatedHandler registra consumo de eventos"
grep -q "processando evento da SQS" src/Handlers/SqsEventConsumerHostedService.cs && echo "  ✅ SqsEventConsumerHostedService registra processamento"
echo ""

# 5. Verificar Configuração Serilog
echo "[5/5] Verificando Configuração Serilog em Program.cs..."
grep -q "LoggerConfiguration" Program.cs && echo "  ✅ LoggerConfiguration configurado"
grep -q "Enrich.FromLogContext" Program.cs && echo "  ✅ LogContext enriquecimento ativado"
grep -q "Enrich.WithEnvironmentName" Program.cs && echo "  ✅ EnvironmentName enriquecimento ativado"
grep -q "Enrich.WithThreadId" Program.cs && echo "  ✅ ThreadId enriquecimento ativado"
grep -q "JsonFormatter" Program.cs && echo "  ✅ JSON formatter configurado"
grep -q "builder.Host.UseSerilog" Program.cs && echo "  ✅ UseSerilog registrado"
echo ""

echo "================================"
echo "✅ Todas as validações concluídas!"
echo "================================"
echo ""
echo "📝 Próximos passos:"
echo "   1. Compilar com: dotnet publish -c Release"
echo "   2. Gerar imagem Docker"
echo "   3. Deploy em EKS"
echo "   4. Validar logs no CloudWatch Logs Insights"
echo ""
