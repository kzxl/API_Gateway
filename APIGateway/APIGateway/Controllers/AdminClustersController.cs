using APIGateway.Core.Interfaces;
using APIGateway.Core.Interfaces.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/clusters")]
public class AdminClustersController : ControllerBase
{
    private readonly IClusterService _service;

    public AdminClustersController(IClusterService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cluster = await _service.GetByIdAsync(id);
        return cluster == null ? NotFound(new { error = "Cluster not found" }) : Ok(cluster);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClusterDto dto) =>
        Ok(await _service.CreateAsync(dto));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateClusterDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result == null ? NotFound(new { error = "Cluster not found" }) : Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id) =>
        await _service.DeleteAsync(id) ? Ok(new { message = "Cluster deleted" }) : NotFound(new { error = "Cluster not found" });
}
