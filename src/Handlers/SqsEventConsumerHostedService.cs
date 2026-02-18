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

                    if (response.Messages.Count == 0)
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
                }
            }
        }

        private async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
        {
            try
            {
                using var document = JsonDocument.Parse(message.Body);
                var root = document.RootElement;

                if (!root.TryGetProperty("Message", out var payloadElement))
                {
                    _logger.LogWarning("Mensagem SQS sem campo 'Message'. Ignorando.");
                    return;
                }

                if (!root.TryGetProperty("MessageAttributes", out var attributesElement))
                {
                    _logger.LogWarning("Mensagem SQS sem 'MessageAttributes'. Ignorando.");
                    return;
                }

                var eventType = GetAttributeValue(attributesElement, "EventType");
                var correlationId = GetAttributeValue(attributesElement, "CorrelationId");
                var causationId = GetAttributeValue(attributesElement, "CausationId");

                if (string.IsNullOrWhiteSpace(eventType))
                {
                    _logger.LogWarning("Mensagem SQS sem EventType. Ignorando.");
                    return;
                }

                var payload = payloadElement.GetString() ?? string.Empty;

                _logger.LogInformation($"→ Processando evento: {eventType} (CorrelationId: {correlationId})");

                using var scope = _serviceProvider.CreateScope();
                var consumer = scope.ServiceProvider.GetRequiredService<IEventConsumer>();
                await consumer.ConsumeAsync(eventType, payload, correlationId, causationId);

                _logger.LogInformation($"✓ Evento {eventType} processado com sucesso");

                await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem SQS");
            }
        }

        private static string GetAttributeValue(JsonElement attributesElement, string key)
        {
            if (!attributesElement.TryGetProperty(key, out var attribute))
            {
                return string.Empty;
            }

            if (!attribute.TryGetProperty("Value", out var valueElement))
            {
                return string.Empty;
            }

            return valueElement.GetString() ?? string.Empty;
        }
    }
}
