using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api/build-info")]
public sealed class BuildInfoController(IConfiguration configuration, IHostEnvironment environment) : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var info = new
        {
            frontendCommit = configuration["BuildInfo:FrontendCommit"] ?? "local",
            backendCommit = configuration["BuildInfo:BackendCommit"] ?? "local",
            deploymentTimestampUtc = configuration["BuildInfo:DeploymentTimestampUtc"],
            environmentName = environment.EnvironmentName
        };

        return Ok(ApiForge.Shared.Responses.Result<object>.Success(info));
    }
}
