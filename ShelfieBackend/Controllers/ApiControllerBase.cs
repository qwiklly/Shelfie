using Microsoft.AspNetCore.Mvc;
using ShelfieBackend.Services;
using System.Security.Claims;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Controllers
{
    /// <summary>
    /// Maps service results to HTTP status codes (400 with a message, 404 when something does not exist).
    /// </summary>
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected IActionResult FromResponse(BaseResponse response) =>
            response.Flag ? Ok(response) : BadRequest(response);

        protected IActionResult FromResult<T>(ServiceResult<T> result) =>
            result.Ok ? Ok(result.Value)
            : result.NotFound ? NotFound(new BaseResponse(false, result.Error!))
            : BadRequest(new BaseResponse(false, result.Error!));

        /// <summary>Id of the authenticated user; endpoints using it are [Authorize], so it is always present.</summary>
        protected int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0
                ? id
                : throw new UnauthorizedAccessException("No user id in token");
    }
}
