using Microsoft.AspNetCore.Mvc;
using static ShelfieBackend.Responses.CustomResponses;

namespace ShelfieBackend.Controllers
{
    /// <summary>
    /// Maps repository responses to HTTP status codes. Previously every failure came back as 200 OK with
    /// Flag = false, so clients that check the status code reported failed operations as successful.
    /// </summary>
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected IActionResult FromResponse(BaseResponse response) =>
            response.Flag ? Ok(response) : BadRequest(response);
    }
}
