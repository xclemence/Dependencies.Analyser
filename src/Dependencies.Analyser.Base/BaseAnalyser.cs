using System.Collections.Generic;
using System.Threading.Tasks;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base
{
    public abstract class BaseAnalyser : IAssemblyAnalyser
    {
        protected BaseAnalyser(IAnalyserSettingProvider settings)
        {
            Settings = settings;
        }

        protected  IAnalyserSettingProvider Settings { get; }

        protected IDictionary<string, AssemblyInformation> AssembliesLoaded { get; } = new Dictionary<string, AssemblyInformation>();
        protected IDictionary<string, AssemblyLink> LinksLoaded { get; } = new Dictionary<string, AssemblyLink>();


        public async Task<(AssemblyInformation assemly, IDictionary<string, AssemblyLink> links)> AnalyseAsync(string dllPath) =>
            await Task.Run(() => Analyse(dllPath)).ConfigureAwait(false);

        protected abstract (AssemblyInformation assembly, IDictionary<string, AssemblyLink> links) Analyse(string dllPath);
    }
}
