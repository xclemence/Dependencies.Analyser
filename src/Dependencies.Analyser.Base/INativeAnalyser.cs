using System.Collections.Generic;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base
{
    public interface INativeAnalyser
    {
        AssemblyInformation GetNative(string dllFile, string baseDirectory);
        IEnumerable<AssemblyLink> GetNativeLinks(string file, string baseDirectory);
        AssemblyInformation LoadNativeAssembly(string entryDll);
    }
}