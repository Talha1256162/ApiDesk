using ApiForge.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result)
    {
        if (result.Succeeded)
        {
            return Ok(result);
        }

        return ToErrorResponse(result);
    }

    protected IActionResult FromResult<T>(Result<T> result)
    {
        if (result.Succeeded)
        {
            return Ok(result);
        }

        return ToErrorResponse(result);
    }

    private IActionResult ToErrorResponse(Result result)
    {
        var firstCode = result.Errors.FirstOrDefault()?.Code;
        return firstCode switch
        {
            "auth.required" => Unauthorized(result),
            "permission.denied" => Forbid(),
            _ => BadRequest(result)
        };
    }
}
