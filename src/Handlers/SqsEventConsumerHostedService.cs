using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;

namespace OFICINACARDOZO.BILLINGSERVICE.Handlers
{
    public class SqsEventConsumerHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly int _pollingIntervalMs;

        public SqsEventConsumerHostedService(IServiceProvider serviceProvider, int pollingIntervalMs = 5000)
        {
            _serviceProvider = serviceProvider;
            _pollingIntervalMs = pollingIntervalMs;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Consumir eventos SQS a cada intervalo
                    await Task.Delay(_pollingIntervalMs, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro no SqsEventConsumerHostedService: {ex.Message}");
                }
            }
        }
    }
}
