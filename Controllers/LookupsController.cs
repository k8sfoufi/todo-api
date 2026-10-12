using Microsoft.AspNetCore.Mvc;
using Todo.Api.Clients;
using Todo.Api.Dtos;

namespace Todo.Api.Controllers;

/// The frontend only talks to this backend; the backend fetches the data from the Info API.
[ApiController]
[Route("api/lookups")]
public class LookupsController(IInfoApiClient info, ILogger<LookupsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LookupsDto>> Get(CancellationToken ct)
    {
        try
        {
            var categories = info.GetCategoriesAsync(ct);
            var priorities = info.GetPrioritiesAsync(ct);
            await Task.WhenAll(categories, priorities);
            return new LookupsDto(categories.Result, priorities.Result);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Info API unavailable");
            return Problem("The info service is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
