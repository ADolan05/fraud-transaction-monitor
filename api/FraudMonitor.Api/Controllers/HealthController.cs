using FraudMonitor.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FraudMonitor.Api.Controllers;

public record HealthResponse(string Status, bool Database, int Customers, int Accounts, int Merchants, long Transactions, int Alerts);

/// <summary>Lets the front end (and Docker) confirm the API and database are up.</summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController(FraudDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken ct)
    {
        if (!await db.Database.CanConnectAsync(ct))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("Unhealthy", false, 0, 0, 0, 0, 0));

        return Ok(new HealthResponse(
            "Healthy",
            true,
            await db.Customers.CountAsync(ct),
            await db.Accounts.CountAsync(ct),
            await db.Merchants.CountAsync(ct),
            await db.Transactions.LongCountAsync(ct),
            await db.Alerts.CountAsync(ct)));
    }
}
