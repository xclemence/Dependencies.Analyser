using System;
using System.Threading.Tasks;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base
{
    public interface IManagedAnalyser
    {
        string EntryDll { get; }

        Task<AssemblyInformation> AnalyseAsync();
    }
}
