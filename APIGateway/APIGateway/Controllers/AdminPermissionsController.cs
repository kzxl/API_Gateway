using APIGateway.Core.Interfaces;
using APIGateway.Infrastructure.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers;

/// <summary>
/// Permission management controller.
/// UArch: Thin adapter over IPermissionService.
/// </summary>
[ApiController]
[Route("admin/permissions")]
public class AdminPermissionsController : ControllerBase
{
    private readonly IPermissionService _service;

    public AdminPermissionsController(IPermissionService service) => _service = service;

    /// <summary>
    /// Get all available permissions.
    /// </summary>
    [HttpGet]
    [RequirePermission("permissions.read")]
    public async Task<IActionResult> GetAll()
    {
        var permissions = await _service.GetAllPermissionsAsync();
        return Ok(permissions);
    }

    /// <summary>
    /// Get permissions for a specific role.
    /// </summary>
    [HttpGet("role/{role}")]
    [RequirePermission("permissions.read")]
    public async Task<IActionResult> GetRolePermissions(string role)
    {
        var permissions = await _service.GetRolePermissionsAsync(role);
        return Ok(permissions);
    }

    /// <summary>
    /// Get permissions for a specific user.
    /// </summary>
    [HttpGet("user/{userId}")]
    [RequirePermission("permissions.read")]
    public async Task<IActionResult> GetUserPermissions(int userId)
    {
        var permissions = await _service.GetUserPermissionsAsync(userId);
        return Ok(permissions);
    }

    /// <summary>
    /// Grant permission to a role.
    /// </summary>
    [HttpPost("role/{role}/grant/{permissionId}")]
    [RequirePermission("permissions.write")]
    public async Task<IActionResult> GrantToRole(string role, int permissionId)
    {
        await _service.GrantPermissionToRoleAsync(role, permissionId);
        return Ok(new { message = "Permission granted to role" });
    }

    /// <summary>
    /// Revoke permission from a role.
    /// </summary>
    [HttpDelete("role/{role}/revoke/{permissionId}")]
    [RequirePermission("permissions.write")]
    public async Task<IActionResult> RevokeFromRole(string role, int permissionId)
    {
        await _service.RevokePermissionFromRoleAsync(role, permissionId);
        return Ok(new { message = "Permission revoked from role" });
    }

    /// <summary>
    /// Grant permission to a user.
    /// </summary>
    [HttpPost("user/{userId}/grant/{permissionId}")]
    [RequirePermission("permissions.write")]
    public async Task<IActionResult> GrantToUser(int userId, int permissionId)
    {
        await _service.GrantPermissionToUserAsync(userId, permissionId);
        return Ok(new { message = "Permission granted to user" });
    }

    /// <summary>
    /// Revoke permission from a user.
    /// </summary>
    [HttpDelete("user/{userId}/revoke/{permissionId}")]
    [RequirePermission("permissions.write")]
    public async Task<IActionResult> RevokeFromUser(int userId, int permissionId)
    {
        await _service.RevokePermissionFromUserAsync(userId, permissionId);
        return Ok(new { message = "Permission revoked from user" });
    }
}
