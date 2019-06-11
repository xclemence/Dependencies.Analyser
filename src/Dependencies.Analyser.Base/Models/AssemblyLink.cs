using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Dependencies.Analyser.Base.Models
{
    [DebuggerDisplay("Assembly = {Assembly.Name}, Version = {LinkVersion}")]
    public class AssemblyLink : MarshalByRefObject, IEquatable<AssemblyLink>
    {
        public AssemblyLink() { }

        public AssemblyLink(AssemblyInformation assembly, string linkVersion)
        {
            Assembly = assembly;
            LinkVersion = linkVersion;
        }

        public AssemblyInformation Assembly { get; set; }

        public string LinkVersion { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as AssemblyLink);
        }

        public bool Equals(AssemblyLink other)
        {
            return other != null &&
                   EqualityComparer<AssemblyInformation>.Default.Equals(Assembly, other.Assembly) &&
                   LinkVersion == other.LinkVersion;
        }

        public override int GetHashCode()
        {
            var hashCode = 320259904;
            hashCode = hashCode * -1521134295 + EqualityComparer<AssemblyInformation>.Default.GetHashCode(Assembly);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(LinkVersion);
            return hashCode;
        }

        public static bool operator ==(AssemblyLink link1, AssemblyLink link2)
        {
            return EqualityComparer<AssemblyLink>.Default.Equals(link1, link2);
        }

        public static bool operator !=(AssemblyLink link1, AssemblyLink link2)
        {
            return !(link1 == link2);
        }
    }
}
