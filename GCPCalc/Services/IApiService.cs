using System.Threading.Tasks;

namespace GCPCalc.Services
{
    public interface IApiService
    {
        Task<string> GetBillingComputeData();

        string GetAccessToken(string jsonKeyFilePath, params string[] scopes);
    }
}