using Microsoft.AspNetCore.Mvc;

namespace OficinaCardozo.API.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    public HealthController()
    {
        Console.WriteLine($"[HealthController] Instanciado em {DateTime.UtcNow:O}");
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        Console.WriteLine($"[HealthController] Live endpoint chamado em {DateTime.UtcNow:O}");
        return Ok(new { status = "Live" });
    }
}