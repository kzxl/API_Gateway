using APIGateway.Models;
using APIGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/clusters")]
public class AdminClustersController : ControllerBase
{
    private readonly IRouteRepository _repo;
    private readonly DbProxyConfigProvider _provider;

    public AdminClustersController(IRouteRepository repo, DbProxyConfigProvider provider)
    {
        _repo = repo;
        _provider = provider;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var clusters = await _repo.GetClustersAsync();
        return Ok(clusters);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cluster = await _repo.GetClusterByIdAsync(id);
        return cluster is null ? NotFound() : Ok(cluster);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Cluster dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ClusterId))
            return BadRequest(new { error = "ClusterId is required" });
        if (string.IsNullOrWhiteSpace(dto.DestinationsJson))
            return BadRequest(new { error = "DestinationsJson is required" });

        dto.Id = 0;
        await _repo.AddOrUpdateClusterAsync(dto);
        _provider.ForceReload();
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Cluster dto)
    {
        var existing = await _repo.GetClusterByIdAsync(id);
        if (existing is null) return NotFound();

        existing.ClusterId = dto.ClusterId;
        existing.DestinationsJson = dto.DestinationsJson;

        await _repo.AddOrUpdateClusterAsync(existing);
        _provider.ForceReload();
        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _repo.DeleteClusterAsync(id);
        _provider.ForceReload();
        return NoContent();
    }
}
