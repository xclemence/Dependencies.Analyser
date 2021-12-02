using System.Collections.Generic;
using System.Threading.Tasks;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base
{
    public interface IAssemblyAnalyser
    {
        Task<(AssemblyInformation assembly, IDictionary<string, AssemblyLink> links)> AnalyseAsync(string dllPath);
    }
}
