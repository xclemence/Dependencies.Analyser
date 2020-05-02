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
    public class AssemblyInformation : IEquatable<AssemblyInformation>
    {
    
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

        public bool IsResolved { get; set; } = true;

        public string FullName => AssemblyName ?? Name;

        public string FilePath { get; set; }

        public bool? IsDebug { get; set; }

        public bool IsILOnly { get; set; }
        
        public string TargetFramework { get; set; }

        public bool HasEntryPoint { get; set; }

        public TargetProcessor? TargetProcessor { get; set; }

        public string Creator { get; set; }

        public DateTime CreationDate { get; set; }

        public List<AssemblyLink> Links { get; set; }

        public override bool Equals(object obj) => Equals(obj as AssemblyInformation);

        public bool Equals(AssemblyInformation other) => 
            other != null &&
            FullName == other.FullName &&
            IsDebug == other.IsDebug &&
            TargetFramework == other.TargetFramework &&
            TargetProcessor == other.TargetProcessor;

        public override int GetHashCode()
        {
            var hashCode = 261209362;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FullName);
            hashCode = hashCode * -1521134295 + IsDebug.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TargetFramework);
            hashCode = hashCode * -1521134295 + TargetProcessor.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(AssemblyInformation information1, AssemblyInformation information2) => 
            EqualityComparer<AssemblyInformation>.Default.Equals(information1, information2);

        public static bool operator !=(AssemblyInformation information1, AssemblyInformation information2) => !(information1 == information2);
    }
}
