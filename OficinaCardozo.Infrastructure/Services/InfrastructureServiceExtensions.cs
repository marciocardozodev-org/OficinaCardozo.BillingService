using Microsoft.Extensions.DependencyInjection;
using OficinaCardozo.Application.Interfaces;
using OficinaCardozo.Infrastructure.Services;

namespace OficinaCardozo.Infrastructure.Services
{
    public static class InfrastructureServiceExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddScoped<IHealthService, HealthService>();
            // Adicione outros serviços de infraestrutura aqui
            return services;
        }
    }
}
