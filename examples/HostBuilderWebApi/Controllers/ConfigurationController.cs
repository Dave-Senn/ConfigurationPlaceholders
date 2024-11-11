using Microsoft.AspNetCore.Mvc;

namespace HostBuilderWebApi.Controllers;

/// <summary>
/// </summary>
[ApiController]
[Route( "[controller]" )]
public class ConfigurationController( IConfiguration configuration ) : ControllerBase
{
    /// <summary>
    /// </summary>
    [HttpGet]
    public IActionResult Get() =>
        Ok( configuration[ "HostInfo" ] );
}