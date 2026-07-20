using System.Net;
using GRLicence;
using Microsoft.Extensions.Configuration;

namespace Declaration.API.Licence;

// TASK-117 : adresse/port/timeout du serveur ApLicence.Server sont des parametres de deploiement
// (varient par client, cf. CDC Sec4) -- lus depuis connections.json (section "ApLicence"), avec
// replis par defaut permissifs (127.0.0.1:8003, 5s) si absents/invalides. Le "subject", lui,
// n'est JAMAIS lu ici -- il est fige en dur dans Program.cs (anti-contournement, GRLicence/TASK-002).
public sealed class GrfLicenceConfigProvider : ILicenceConfigProvider
{
    private const string DefaultAddress = "127.0.0.1";
    private const int DefaultPort = 8003;
    private const int DefaultTimeoutSeconds = 5;

    private readonly IConfiguration _configuration;

    public GrfLicenceConfigProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ServerConnectionConfig GetConfig()
    {
        if (!IPAddress.TryParse(_configuration["ApLicence:ServerAddress"], out var address))
            address = IPAddress.Parse(DefaultAddress);

        var port = _configuration.GetValue<int?>("ApLicence:ServerPort") ?? DefaultPort;
        if (port <= 0 || port > 65535)
            port = DefaultPort;

        var timeoutSeconds = _configuration.GetValue<int?>("ApLicence:TimeoutSeconds") ?? DefaultTimeoutSeconds;
        if (timeoutSeconds <= 0)
            timeoutSeconds = DefaultTimeoutSeconds;

        return new ServerConnectionConfig(address, port, System.TimeSpan.FromSeconds(timeoutSeconds));
    }
}
