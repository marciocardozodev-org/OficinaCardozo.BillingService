using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.API.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly OficinaDbContext _context;

    public HealthController(OficinaDbContext context)
    {
        _context = context;
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        // Rota simples que não depende de nada
        return Ok(new
        {
            status = "Alive",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            lambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"))
        });
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Timeout de 5 segundos para não travar
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var canConnect = await _context.Database.CanConnectAsync(cts.Token);

            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                database = canConnect ? "Connected" : "Disconnected"
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(503, new
            {
                status = "Unhealthy",
                timestamp = DateTime.UtcNow,
                error = "Database connection timeout (5s)",
                database = "Timeout"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "Unhealthy",
                timestamp = DateTime.UtcNow,
                error = ex.Message,
                errorType = ex.GetType().Name,
                database = "Disconnected"
            });
        }
    }

    public class DatadogMetricRequest
    {
        public string MetricName { get; set; }
        public double Value { get; set; }
        public string Host { get; set; }
        public string[] Tags { get; set; }
    }

    [HttpPost("datadog-metric")]
    public async Task<IActionResult> SendDatadogMetric([FromBody] DatadogMetricRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MetricName))
            return BadRequest(new { error = "Invalid metric data" });

        var datadog = new Integrations.DatadogApiClient();
        await datadog.SendMetricAsync(
            metricName: request.MetricName,
            value: request.Value,
            host: string.IsNullOrWhiteSpace(request.Host) ? Environment.MachineName : request.Host,
            tags: request.Tags ?? new[] { "env:prod", "service:api", "controller:Health" }
        );
        return Ok(new { status = "Metric sent to Datadog" });
    }
}