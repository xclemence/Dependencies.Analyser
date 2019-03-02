using System.Threading.Tasks;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base
{
    public interface IAssemblyAnalyser
    {
        Task<AssemblyInformation> AnalyseAsync(string dllPath);
    }
}
