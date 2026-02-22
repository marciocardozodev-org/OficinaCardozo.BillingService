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
                    using var document = JsonDocument.Parse(payload);
                    var root = document.RootElement;
                    var payloadElement = root.TryGetProperty("Payload", out var nestedPayload)
                        ? nestedPayload
                        : root;

                    var osCreated = new OsCreated
                    {
                        OsId = ParseOsId(payloadElement),
                        Description = payloadElement.TryGetProperty("Description", out var description)
                            || payloadElement.TryGetProperty("description", out description)
                            || payloadElement.TryGetProperty("descricao", out description)
                            ? description.GetString() ?? string.Empty
                            : string.Empty,
                        CreatedAt = TryGetDateTime(payloadElement, "CreatedAt")
                            ?? TryGetDateTime(payloadElement, "createdAt")
                            ?? TryGetDateTime(payloadElement, "timestamp")
                            ?? DateTime.UtcNow,
                        Valor = TryGetDecimal(payloadElement, "Valor")
                            ?? TryGetDecimal(payloadElement, "valor")
                    };

                    var envelope = new EventEnvelope<OsCreated>
                    {
                        CorrelationId = TryParseGuid(root, "CorrelationId")
                            ?? TryParseGuid(correlationId)
                            ?? Guid.NewGuid(),
                        CausationId = TryParseGuid(root, "CausationId")
                            ?? TryParseGuid(causationId)
                            ?? Guid.NewGuid(),
                        Timestamp = DateTime.UtcNow,
                        Payload = osCreated
                    };

                    await _osCreatedHandler.HandleAsync(envelope);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar evento {eventType}: {ex.Message}");
                throw;
            }
        }

        private static Guid ParseOsId(JsonElement payloadElement)
        {
            // Tenta ambos: OsId (Pascal) e osId (camel) para compatibilidade
            if (!payloadElement.TryGetProperty("OsId", out var osIdElement) &&
                !payloadElement.TryGetProperty("osId", out osIdElement))
            {
                return Guid.Empty;
            }

            if (osIdElement.ValueKind == JsonValueKind.String)
            {
                var osIdText = osIdElement.GetString();
                if (Guid.TryParse(osIdText, out var guidValue))
                {
                    return guidValue;
                }
                if (long.TryParse(osIdText, out var numericValue))
                {
                    return NumericOsIdToGuid(numericValue);
                }
            }

            if (osIdElement.ValueKind == JsonValueKind.Number && osIdElement.TryGetInt64(out var osIdNumber))
            {
                return NumericOsIdToGuid(osIdNumber);
            }

            return Guid.Empty;
        }

        private static Guid NumericOsIdToGuid(long osId)
        {
            var suffix = osId.ToString().PadLeft(12, '0');
            return Guid.Parse($"00000000-0000-0000-0000-{suffix}");
        }

        private static Guid? TryParseGuid(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var valueElement))
            {
                return null;
            }

            var value = valueElement.GetString();
            return Guid.TryParse(value, out var parsed) ? parsed : null;
        }

        private static Guid? TryParseGuid(string value)
        {
            return Guid.TryParse(value, out var parsed) ? parsed : null;
        }

        private static DateTime? TryGetDateTime(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var valueElement))
            {
                return null;
            }

            if (valueElement.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(valueElement.GetString(), out var parsed))
            {
                return parsed;
            }

            if (valueElement.ValueKind == JsonValueKind.Number &&
                valueElement.TryGetInt64(out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }

            return null;
        }

        private static decimal? TryGetDecimal(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var valueElement))
            {
                return null;
            }

            if (valueElement.ValueKind == JsonValueKind.Number)
            {
                if (valueElement.TryGetDecimal(out var decimalValue))
                {
                    return decimalValue;
                }
                if (valueElement.TryGetDouble(out var doubleValue))
                {
                    return (decimal)doubleValue;
                }
            }

            if (valueElement.ValueKind == JsonValueKind.String)
            {
                var stringValue = valueElement.GetString();
                if (decimal.TryParse(stringValue, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }
    }
}
