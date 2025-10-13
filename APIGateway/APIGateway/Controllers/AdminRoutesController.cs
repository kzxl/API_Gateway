using APIGateway.Models;
using APIGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
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
            var clusters = await _repo.GetClustersAsync();
            return Ok(new { routes, clusters });
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Models.Route dto)
        {
            await _repo.AddOrUpdateRouteAsync(dto);
            _provider.ForceReload();
            return CreatedAtAction(nameof(GetAll), null);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _repo.DeleteRouteAsync(id);
            _provider.ForceReload();
            return NoContent();
        }
    }
}
