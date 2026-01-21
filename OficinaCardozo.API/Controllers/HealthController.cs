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

}