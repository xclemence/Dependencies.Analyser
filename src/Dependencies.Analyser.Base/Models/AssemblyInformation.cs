using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Dependencies.Analyser.Base.Models
{
    public enum TargetProcessor
    {
        AnyCpu,
        x86,
        x64,
    }

    [DebuggerDisplay("Name = {Name}, Loaded Version = {LoadedVersion}, Local= {IsLocalAssembly}, Resolved={IsResolved} , Links = {Links.Count}")]
    public class AssemblyInformation : MarshalByRefObject, IEquatable<AssemblyInformation>
    {

        public AssemblyInformation() { }

        public AssemblyInformation(string name,
                                   string loadedVersion,
                                   string filePath)
        {
            Name = name;
            LoadedVersion = loadedVersion;
            Links = new List<AssemblyLink>();
            FilePath = filePath;
        }

        public string Name { get; set;  }
        public string LoadedVersion { get; set; }

        public string AssemblyName { get; set; }

        public bool IsLocalAssembly { get; set; }

        public bool IsNative { get; set; }

        public bool IsResolved => !string.IsNullOrEmpty(FilePath) || AssemblyName != null;

        public string FullName => AssemblyName ?? Name;

        public string FilePath { get; set; }

        public bool? IsDebug { get; set; }

        public bool IsILOnly { get; set; }

        public TargetProcessor? TargetProcessor { get; set; }

        public string Creator { get; set; }

        public DateTime CreationDate { get; set; }

        public List<AssemblyLink> Links { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as AssemblyInformation);
        }

        public bool Equals(AssemblyInformation other)
        {
            return other != null &&
                   Name == other.Name &&
                   LoadedVersion == other.LoadedVersion &&
                   AssemblyName == other.AssemblyName &&
                   IsLocalAssembly == other.IsLocalAssembly &&
                   IsNative == other.IsNative &&
                   IsResolved == other.IsResolved &&
                   FullName == other.FullName &&
                   FilePath == other.FilePath &&
                   EqualityComparer<bool?>.Default.Equals(IsDebug, other.IsDebug) &&
                   IsILOnly == other.IsILOnly &&
                   EqualityComparer<TargetProcessor?>.Default.Equals(TargetProcessor, other.TargetProcessor) &&
                   Creator == other.Creator &&
                   CreationDate == other.CreationDate &&
                   EqualityComparer<List<AssemblyLink>>.Default.Equals(Links, other.Links);
        }

        public override int GetHashCode()
        {
            var hashCode = -81212879;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(LoadedVersion);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssemblyName);
            hashCode = hashCode * -1521134295 + IsLocalAssembly.GetHashCode();
            hashCode = hashCode * -1521134295 + IsNative.GetHashCode();
            hashCode = hashCode * -1521134295 + IsResolved.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FullName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FilePath);
            hashCode = hashCode * -1521134295 + EqualityComparer<bool?>.Default.GetHashCode(IsDebug);
            hashCode = hashCode * -1521134295 + IsILOnly.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<TargetProcessor?>.Default.GetHashCode(TargetProcessor);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Creator);
            hashCode = hashCode * -1521134295 + CreationDate.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<List<AssemblyLink>>.Default.GetHashCode(Links);
            return hashCode;
        }

        public static bool operator ==(AssemblyInformation information1, AssemblyInformation information2)
        {
            return EqualityComparer<AssemblyInformation>.Default.Equals(information1, information2);
        }

        public static bool operator !=(AssemblyInformation information1, AssemblyInformation information2)
        {
            return !(information1 == information2);
        }
    }
}
