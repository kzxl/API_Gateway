using APIGateway.Models;
using APIGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/routes")]
public class AdminRoutesController : ControllerBase
{
    private readonly IRouteRepository _repo;
    private readonly DbProxyConfigProvider _provider;

    public AdminRoutesController(IRouteRepository repo, DbProxyConfigProvider provider)
    {
        _repo = repo;
        _provider = provider;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var routes = await _repo.GetRoutesAsync();
        return Ok(routes);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var route = await _repo.GetRouteByIdAsync(id);
        return route is null ? NotFound() : Ok(route);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Models.Route dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RouteId))
            return BadRequest(new { error = "RouteId is required" });
        if (string.IsNullOrWhiteSpace(dto.MatchPath))
            return BadRequest(new { error = "MatchPath is required" });
        if (string.IsNullOrWhiteSpace(dto.ClusterId))
            return BadRequest(new { error = "ClusterId is required" });

        dto.Id = 0; // force create
        await _repo.AddOrUpdateRouteAsync(dto);
        _provider.ForceReload();
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Models.Route dto)
    {
        var existing = await _repo.GetRouteByIdAsync(id);
        if (existing is null) return NotFound();

        existing.RouteId = dto.RouteId;
        existing.MatchPath = dto.MatchPath;
        existing.Methods = dto.Methods;
        existing.ClusterId = dto.ClusterId;

        await _repo.AddOrUpdateRouteAsync(existing);
        _provider.ForceReload();
        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _repo.DeleteRouteAsync(id);
        _provider.ForceReload();
        return NoContent();
    }
}
