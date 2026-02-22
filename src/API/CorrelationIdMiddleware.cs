using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System;
using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.API
{
    /// <summary>
    /// Middleware que gerencia CorrelationId ponta a ponta.
    /// 
    /// Responsabilidades:
    /// 1. Ler Correlation-Id do header da request (se existir)
    /// 2. Se não existir, gerar um novo GUID
    /// 3. Armazenar no HttpContext.Items
    /// 4. Enriquecer logs com LogContext.PushProperty
    /// 5. Retornar CorrelationId no response header
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CorrelationIdHeaderName = "Correlation-Id";
        private const string CorrelationIdPropertyName = "CorrelationId";

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 1. Tentar ler do header, ou gerar novo GUID
            string correlationId = context.Request.Headers[CorrelationIdHeaderName].ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            // 2. Armazenar no HttpContext.Items para acesso em componentes
            context.Items[CorrelationIdPropertyName] = correlationId;

            // 3. Enriquecer logs com LogContext (Serilog)
            using (LogContext.PushProperty(CorrelationIdPropertyName, correlationId))
            {
                // 4. Adicionar ao response header
                if (!context.Response.HasStarted)
                {
                    context.Response.Headers[CorrelationIdHeaderName] = correlationId;
                }

                // 5. Continuar pipeline
                await _next(context);
            }
        }
    }

    /// <summary>
    /// Helper para extrair CorrelationId do HttpContext
    /// </summary>
    public static class CorrelationIdExtensions
    {
        public const string CorrelationIdPropertyName = "CorrelationId";

        public static string GetCorrelationId(this HttpContext context)
        {
            return context.Items.TryGetValue(CorrelationIdPropertyName, out var value)
                ? value?.ToString() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();
        }
    }
}
