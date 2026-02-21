using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    /// <summary>
    /// ✅ TRANSACTIONAL OUTBOX PATTERN - Fase 2
    /// 
    /// Background Service que periodicamente:
    /// 1. Query OutboxMessages onde published = false
    /// 2. Publica cada mensagem em SNS
    /// 3. Marca como published = true
    /// 
    /// Garantias:
    /// - Resiliência: Se publicação falhar, terá retry automático
    /// - Entrega Eventual: Eventualmente todos eventos serão entregues
    /// - Atomicidade: DB update e publicação são tratados separadamente
    /// </summary>
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;
        private static readonly TimeSpan ProcessInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 OutboxProcessor iniciado. Processando mensagens a cada {Interval}ms", ProcessInterval.TotalMilliseconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                    await Task.Delay(ProcessInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("OutboxProcessor foi cancelado");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro crítico no OutboxProcessor");
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }

            _logger.LogInformation("⏹️ OutboxProcessor parado");
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
                var snsClient = scope.ServiceProvider.GetRequiredService<IAmazonSimpleNotificationService>();
                var snsTopics = scope.ServiceProvider.GetRequiredService<SnsTopicConfiguration>();

                // 1. Query mensagens não publicadas (ordenado por mais antigas primeiro)
                var unpublishedMessages = await dbContext.Set<OutboxMessage>()
                    .AsNoTracking()  // ✅ Disable change tracking for read-only query
                    .Where(m => !m.Published)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync(stoppingToken);

                if (unpublishedMessages.Count == 0)
                {
                    _logger.LogDebug("📭 Nenhuma mensagem Outbox pendente no momento");
                    return; // Nada para fazer
                }

                _logger.LogInformation("📤 Processando {Count} mensagens Outbox não publicadas", unpublishedMessages.Count);

                // 2. Para cada mensagem, tentar publicar
                foreach (var message in unpublishedMessages)
                {
                    try
                    {
                        var snsMessageId = await PublishOutboxMessageAsync(message, snsClient, snsTopics, stoppingToken);

                        // 3. Marcar como publicado sem regravar DateTime Unspecified
                        var publishedAtUtc = DateTime.UtcNow;
                        await dbContext.OutboxMessages
                            .Where(m => m.Id == message.Id)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(m => m.Published, true)
                                .SetProperty(m => m.PublishedAt, publishedAtUtc), stoppingToken);

                        _logger.LogInformation(
                            "✅ OutboxMessage {MessageId} ({EventType}) publicada com sucesso. CorrelationId: {CorrelationId}. SnsMessageId: {SnsMessageId}",
                            message.Id,
                            message.EventType,
                            message.CorrelationId,
                            snsMessageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "❌ Erro ao publicar OutboxMessage {MessageId} ({EventType}). Será retentado na próxima execução.",
                            message.Id,
                            message.EventType);
                        // NÃO marca como published - vai tentar novamente na próxima execução
                    }
                }
            }
        }

        private async Task<string> PublishOutboxMessageAsync(
            OutboxMessage message,
            IAmazonSimpleNotificationService snsClient,
            SnsTopicConfiguration snsTopics,
            CancellationToken stoppingToken)
        {
            // Determinar qual SNS Topic usar baseado no event_type
            string topicArn = message.EventType switch
            {
                nameof(BudgetGenerated) => snsTopics.BudgetGeneratedTopicArn,
                nameof(BudgetApproved) => snsTopics.BudgetApprovedTopicArn,
                nameof(BudgetRejected) => snsTopics.BudgetRejectedTopicArn,
                nameof(PaymentPending) => snsTopics.PaymentPendingTopicArn,
                nameof(PaymentConfirmed) => snsTopics.PaymentConfirmedTopicArn,
                nameof(PaymentFailed) => snsTopics.PaymentFailedTopicArn,
                nameof(PaymentReversed) => snsTopics.PaymentReversedTopicArn,
                _ => throw new InvalidOperationException($"Tipo de evento desconhecido: {message.EventType}")
            };

            // Publicar em SNS
            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message.Payload,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {
                        "EventType",
                        new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = message.EventType
                        }
                    },
                    {
                        "CorrelationId",
                        new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = message.CorrelationId.ToString()
                        }
                    },
                    {
                        "CausationId",
                        new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = message.CausationId.ToString()
                        }
                    },
                    {
                        "Timestamp",
                        new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = DateTime.UtcNow.ToString("o")
                        }
                    }
                }
            };

            var response = await snsClient.PublishAsync(request, stoppingToken);

            _logger.LogDebug(
                "📨 Evento publicado em SNS. MessageId: {SnsMessageId}, TopicArn: {TopicArn}",
                response.MessageId,
                topicArn);

            return response.MessageId ?? string.Empty;
        }
    }

    /// <summary>
    /// Configuração de SNS Topics carregada das variáveis de ambiente
    /// (espelhando padrão do OSService)
    /// </summary>
    public class SnsTopicConfiguration
    {
        public string BudgetGeneratedTopicArn { get; set; } = string.Empty;
        public string BudgetApprovedTopicArn { get; set; } = string.Empty;
        public string BudgetRejectedTopicArn { get; set; } = string.Empty;
        public string PaymentPendingTopicArn { get; set; } = string.Empty;
        public string PaymentConfirmedTopicArn { get; set; } = string.Empty;
        public string PaymentFailedTopicArn { get; set; } = string.Empty;
        public string PaymentReversedTopicArn { get; set; } = string.Empty;
    }
}
