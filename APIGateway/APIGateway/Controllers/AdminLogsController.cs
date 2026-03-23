using APIGateway.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/logs")]
public class AdminLogsController : ControllerBase
{
    private readonly ILogService _service;

    public AdminLogsController(ILogService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? routeId = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] string? method = null) =>
        Ok(await _service.GetLogsAsync(page, pageSize, routeId, statusCode, method));

    [HttpDelete]
    public async Task<IActionResult> ClearLogs()
    {
        await _service.ClearAsync();
        return Ok(new { message = "Logs cleared" });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats() => Ok(await _service.GetStatsAsync());
}
