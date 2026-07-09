using System.Threading.Tasks;
using SageTaxReader.Contracts;

namespace Declaration.Core
{
    public interface ILecteurTvaFgr
    {
        DocumentTaxesInfo? LireTvaFgr(int ecId, string numeroFacture, string connectionString, string sageConnectionString);
    }
}
