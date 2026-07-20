using System.Data;
using System.Threading.Tasks;

namespace Declaration.Application.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateGrfConnection();
    string GetGrfConnectionString();
    IDbConnection CreatePersistenceConnection();

    /// <summary>
    /// TASK-118 : résout dynamiquement la connexion Sage pour une société (<paramref name="soId"/>)
    /// en lisant <c>P_SOCIETE.SO_ErpDb</c> (nom de base) + <c>SO_ErpUserApp</c>/<c>SO_ErpPasswdApp</c>
    /// (identifiants Sage OM) depuis <c>GrfConnection</c>. Le serveur SQL, le login et le mot de
    /// passe de la connexion Sage restent EXCLUSIVEMENT ceux de <c>GrfConnection</c> — seule la
    /// base (<c>Database</c>) change, remplacée par <c>SO_ErpDb</c> (confirmation PO 18/07/2026).
    /// Échec explicite (exception), jamais de repli silencieux, si le <paramref name="soId"/> est
    /// introuvable dans <c>P_SOCIETE</c> ou si <c>SO_ErpDb</c> est vide/NULL.
    /// </summary>
    Task<SageConnectionInfo> GetSageConnectionInfoAsync(int soId);
}

/// <summary>
/// TASK-118 : chaîne de connexion Sage résolue pour une société donnée + identifiants Sage OM
/// (Objets Métier, distincts du login SQL) associés à cette même société.
/// </summary>
public class SageConnectionInfo
{
    public string ConnectionString { get; init; } = "";
    public string? OmUser { get; init; }
    public string? OmPassword { get; init; }
}
