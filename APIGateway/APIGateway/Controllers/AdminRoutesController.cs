using APIGateway.Core.Interfaces;
using APIGateway.Core.Interfaces.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

/// <summary>
/// Routes CRUD — thin wrapper over IRouteService.
/// UArch: Controller is adapter, Service is brain.
/// </summary>
[ApiController]
[Route("admin/routes")]
public class AdminRoutesController : ControllerBase
{
    private readonly IRouteService _service;

    public AdminRoutesController(IRouteService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var route = await _service.GetByIdAsync(id);
        return route == null ? NotFound(new { error = "Route not found" }) : Ok(route);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRouteDto dto) =>
        Ok(await _service.CreateAsync(dto));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateRouteDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result == null ? NotFound(new { error = "Route not found" }) : Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id) =>
        await _service.DeleteAsync(id) ? Ok(new { message = "Route deleted" }) : NotFound(new { error = "Route not found" });
}
