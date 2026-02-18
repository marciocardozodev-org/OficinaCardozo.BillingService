using Amazon.SQS;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Handlers;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    public class SqsEventConsumerImpl : IEventConsumer
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;
        private readonly OsCreatedHandler _osCreatedHandler;

        public SqsEventConsumerImpl(IAmazonSQS sqs, string queueUrl, OsCreatedHandler osCreatedHandler)
        {
            _sqs = sqs;
            _queueUrl = queueUrl;
            _osCreatedHandler = osCreatedHandler;
        }

        public async Task ConsumeAsync(string eventType, string payload, string correlationId, string causationId)
        {
            try
            {
                if (eventType == nameof(OsCreated))
                {
                    var osCreated = JsonSerializer.Deserialize<OsCreated>(payload);
                    if (osCreated != null)
                    {
                        var envelope = new EventEnvelope<OsCreated>
                        {
                            CorrelationId = Guid.Parse(correlationId),
                            CausationId = Guid.Parse(causationId),
                            Timestamp = DateTime.UtcNow,
                            Payload = osCreated
                        };
                        await _osCreatedHandler.HandleAsync(envelope);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar evento {eventType}: {ex.Message}");
                throw;
            }
        }
    }
}
