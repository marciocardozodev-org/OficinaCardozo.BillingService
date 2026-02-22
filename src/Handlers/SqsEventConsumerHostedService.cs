using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;

namespace OFICINACARDOZO.BILLINGSERVICE.Handlers
{
    public class SqsEventConsumerHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly int _pollingIntervalMs;
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;
        private readonly ILogger<SqsEventConsumerHostedService> _logger;

        public SqsEventConsumerHostedService(
            IServiceProvider serviceProvider,
            IAmazonSQS sqs,
            ILogger<SqsEventConsumerHostedService> logger,
            int pollingIntervalMs = 5000)
        {
            _serviceProvider = serviceProvider;
            _sqs = sqs;
            _logger = logger;
            _pollingIntervalMs = pollingIntervalMs;
            _queueUrl = Environment.GetEnvironmentVariable("AWS_SQS_QUEUE_BILLING")
                ?? "http://localhost:4566/000000000000/billing-events";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"✓ SqsEventConsumerHostedService iniciado. QueueUrl: {_queueUrl}");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 5,
                        MessageAttributeNames = new List<string> { "All" }
                    }, stoppingToken);

                    if (response?.Messages == null || response.Messages.Count == 0)
                    {
                        await Task.Delay(_pollingIntervalMs, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation($"✓ Recebidas {response.Messages.Count} mensagens da SQS");

                    foreach (var message in response.Messages)
                    {
                        await ProcessMessageAsync(message, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no SqsEventConsumerHostedService");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
        {
            string? eventType = null;
            string? correlationId = null;
            string? causationId = null;

            try
            {
                JsonDocument? document = null;
                JsonElement root;

                try
                {
                    document = JsonDocument.Parse(message.Body);
                    root = document.RootElement;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "✗ Mensagem SQS inválida (JSON malformado). Deletando.");
                    await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    return;
                }

                // Log da estrutura da mensagem para debugging
                _logger.LogInformation($"→ Estrutura da mensagem: {document}");

                // Verificar se é um envelope SNS (publicado por SNS em SQS)
                string payload;
                if (root.TryGetProperty("Message", out var messageElement))
                {
                    payload = messageElement.GetString() ?? string.Empty;
                }
                else
                {
                    // Fallback: mensagem crua (raw) direto na SQS
                    _logger.LogWarning("✗ Mensagem SQS sem campo 'Message' (não é envelope SNS). Tentando tratar como payload bruto.");
                    payload = message.Body;
                }
                
                // Tentar extrair attributes do SNS MessageAttributes (no envelope SNS)
                if (root.TryGetProperty("MessageAttributes", out var attributesElement))
                {
                    _logger.LogInformation($"→ Encontrados MessageAttributes no envelope SNS");
                    eventType = ExtractSnsAttribute(attributesElement, "EventType");
                    correlationId = ExtractSnsAttribute(attributesElement, "CorrelationId");
                    causationId = ExtractSnsAttribute(attributesElement, "CausationId");
                }

                // Se não encontrou no envelope SNS, tentar nos attributes do SQS (message.MessageAttributes)
                if (string.IsNullOrWhiteSpace(eventType) && message.MessageAttributes!=null && message.MessageAttributes.Count > 0)
                {
                    _logger.LogInformation($"→ Tentando extrair de SQS MessageAttributes");
                    if (message.MessageAttributes.TryGetValue("EventType", out var attr))
                    {
                        eventType = attr.StringValue;
                    }
                    if (message.MessageAttributes.TryGetValue("CorrelationId", out var corrAttr))
                    {
                        correlationId = corrAttr.StringValue;
                    }
                    if (message.MessageAttributes.TryGetValue("CausationId", out var causAttr))
                    {
                        causationId = causAttr.StringValue;
                    }
                }

                if (string.IsNullOrWhiteSpace(eventType))
                {
                    // Fallback: tentar extrair do payload bruto
                    eventType = TryGetStringProperty(root, "EventType") ??
                                TryGetStringProperty(root, "Type") ??
                                TryGetStringProperty(root, "eventType");
                    correlationId ??= TryGetStringProperty(root, "CorrelationId");
                    causationId ??= TryGetStringProperty(root, "CausationId");
                }

                if (string.IsNullOrWhiteSpace(eventType))
                {
                    _logger.LogWarning("✗ EventType não encontrado em nenhuma fonte. Deletando mensagem.");
                    await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    return;
                }

                _logger.LogInformation(
                    "📥 BillingService processando evento da SQS. EventType: {EventType}, CorrelationId: {CorrelationId}, MessageId: {MessageId}, CausationId: {CausationId}",
                    eventType, correlationId, message.MessageId, causationId);

                using var scope = _serviceProvider.CreateScope();
                var consumer = scope.ServiceProvider.GetRequiredService<IEventConsumer>();
                await consumer.ConsumeAsync(eventType, payload, correlationId ?? string.Empty, causationId ?? string.Empty);

                _logger.LogInformation(
                    "✅ BillingService consumiu evento com sucesso. EventType: {EventType}, CorrelationId: {CorrelationId}, MessageId: {MessageId}, Status: Processado",
                    eventType, correlationId, message.MessageId);

                await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "❌ Erro ao processar mensagem da SQS. EventType: {EventType}, CorrelationId: {CorrelationId}, MessageId: {MessageId}, Erro: {ErrorMessage}",
                    eventType ?? "Desconhecido",
                    correlationId ?? "N/A",
                    message?.MessageId ?? "N/A",
                    ex.Message);
            }
        }

        private static string? ExtractSnsAttribute(JsonElement attributesElement, string key)
        {
            if (!attributesElement.TryGetProperty(key, out var attribute))
            {
                return null;
            }

            // SNS MessageAttributes estrutura: { "Value": "...", "Type": "String|Number" }
            if (attribute.TryGetProperty("Value", out var valueElement))
            {
                return valueElement.GetString();
            }

            // Tenta ler direto como string também
            return attribute.GetString();
        }

        private static string? TryGetStringProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var valueElement))
            {
                return null;
            }

            return valueElement.ValueKind switch
            {
                JsonValueKind.String => valueElement.GetString(),
                JsonValueKind.Number => valueElement.GetRawText(),
                _ => null
            };
        }
    }
}
