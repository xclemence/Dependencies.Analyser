using System;
using System.Diagnostics;

namespace Dependencies.Analyser.Base.Models
{
    [DebuggerDisplay("Assembly = {Assembly.Name}, Version = {LinkVersion}")]
    public class AssemblyLink : MarshalByRefObject
    {
        public AssemblyLink(AssemblyInformation assembly, string linkVersion)
        {
            Assembly = assembly;
            LinkVersion = linkVersion;
        }

        public AssemblyInformation Assembly { get; }

        public string LinkVersion { get; }
    }
}
