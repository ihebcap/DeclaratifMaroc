using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Declaration.Controle;

public class DeclarationGrfnInfo
{
    public int DT_Id { get; set; }
    public int SocieteId { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
}

public interface IGrfnDeclarationRepository
{
    Task<DeclarationGrfnInfo?> GetDeclarationInfoAsync(int dtId);
    Task<IEnumerable<LigneDeclarationGrfn>> GetLignesDeclarationAsync(int dtId);
}
