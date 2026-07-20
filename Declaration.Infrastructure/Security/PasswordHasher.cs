using System.Security.Cryptography;
using System.Text;

namespace Declaration.Infrastructure.Security;

/// <summary>
/// TASK-115 : réimplémentation native du hachage legacy
/// (Tresorerie.Infrastructure.PasswordHasher, D:\_vibe\apbs-gr_winform\src\Tresorerie.Infrastructure),
/// pour éliminer la dépendance binaire à Tresorerie.*/System.Data.SqlClient dans Declaration.API.
/// Algorithme et clé identiques à l'original (HMACSHA512, clé fixe) : impératif pour rester
/// compatible avec les hachages UT_HASH/UT_SALT déjà stockés dans P_UTILISATEUR — un changement de
/// clé ou d'algorithme invaliderait tous les mots de passe existants.
/// </summary>
public static class PasswordHasher
{
    private static readonly HMACSHA512 HashKey = new(Encoding.UTF8.GetBytes("P@ssw0rd-@PBS-HB_HG_AA_IC_2@2@!"));

    public static byte[] Hash(string password, byte[] salt)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var allBytes = new byte[bytes.Length + salt.Length];
        Buffer.BlockCopy(bytes, 0, allBytes, 0, bytes.Length);
        Buffer.BlockCopy(salt, 0, allBytes, bytes.Length, salt.Length);
        return HashKey.ComputeHash(allBytes);
    }
}
