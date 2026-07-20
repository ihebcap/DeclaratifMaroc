using System.Net;
using System.Net.Sockets;

namespace Declaration.Setup.Services;

/// <summary>
/// Vérifie qu'un port est libre avant l'installation/démarrage du service (TASK-115 §Livrables :
/// "port déjà occupé par un autre process : message d'erreur clair, pas de crash silencieux").
/// </summary>
public static class PortAvailability
{
    public static bool IsFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
