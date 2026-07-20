using GRLicence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.API.Controllers;

// TASK-117 : seul point de lecture du LicenceStatus expose au front (bannière J-30 + message de
// blocage, cf. CDC Sec1.3/1.4). Volontairement exclu du middleware de blocage (Program.cs) --
// sans cet endpoint toujours accessible, le front ne pourrait jamais savoir pourquoi il est bloqué.
[ApiController]
[Route("api/licence")]
[AllowAnonymous]
public class LicenceController : ControllerBase
{
    private readonly LicenceMonitor _licenceMonitor;

    public LicenceController(LicenceMonitor licenceMonitor)
    {
        _licenceMonitor = licenceMonitor;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _licenceMonitor.GetStatus();
        return Ok(new
        {
            EstValide = status.EstValide,
            Message = status.Message,
            AlerteProcheExpiration = status.AlerteProcheExpiration,
            JoursRestants = status.JoursRestants,
            DateExpiration = status.DateExpiration
        });
    }
}
