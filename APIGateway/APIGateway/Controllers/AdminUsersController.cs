using APIGateway.Core.Interfaces;
using APIGateway.Core.Interfaces.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _service;

    public AdminUsersController(IUserService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto) =>
        Ok(await _service.CreateAsync(dto));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result == null ? NotFound(new { error = "User not found" }) : Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id) =>
        await _service.DeleteAsync(id) ? Ok(new { message = "User deleted" }) : NotFound(new { error = "User not found" });
}
