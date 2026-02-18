using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;
using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    public class SqsEventPublisher : IEventPublisher
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;

        public SqsEventPublisher(IAmazonSQS sqs, string queueUrl)
        {
            _sqs = sqs;
            _queueUrl = queueUrl;
        }

        public async Task PublishAsync<T>(Contracts.Events.EventEnvelope<T> envelope)
        {
            var messageBody = JsonSerializer.Serialize(envelope);
            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = messageBody
            };
            await _sqs.SendMessageAsync(request);
        }
    }
}
