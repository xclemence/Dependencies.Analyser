using System.Collections.Generic;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base
{
    public interface INativeAnalyser
    {
        IEnumerable<AssemblyLink> GetNativeLinks(string file, string baseDirectory);
        AssemblyInformation LoadNativeAssembly(string entryDll);
        AssemblyLink GetNativeLink(string dllName, string baseDirectory);
    }
}