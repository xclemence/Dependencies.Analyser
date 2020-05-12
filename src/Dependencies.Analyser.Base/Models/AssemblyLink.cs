using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Dependencies.Analyser.Base.Models
{
    [DebuggerDisplay("Assembly = {Assembly.Name}, Version = {LinkVersion}")]
    public class AssemblyLink : IEquatable<AssemblyLink>
    {
        public AssemblyLink(AssemblyInformation assembly, string linkVersion, string linkFullName)
        {
            Assembly = assembly;
            LinkVersion = linkVersion;
            LinkFullName = linkFullName;
        }

        public AssemblyInformation Assembly { get; set; }

        public string LinkVersion { get; set; }

        public string LinkFullName { get; set; }

        public override bool Equals(object? obj) => obj is AssemblyLink link && Equals(link);
        public bool Equals(AssemblyLink other) => EqualityComparer<AssemblyInformation>.Default.Equals(Assembly, other.Assembly) && LinkFullName == other.LinkFullName;
        public override int GetHashCode() => HashCode.Combine(Assembly, LinkFullName);

        public static bool operator ==(AssemblyLink? left, AssemblyLink? right) => EqualityComparer<AssemblyLink?>.Default.Equals(left, right);
        public static bool operator !=(AssemblyLink? left, AssemblyLink? right) => !(left == right);
    }
}
