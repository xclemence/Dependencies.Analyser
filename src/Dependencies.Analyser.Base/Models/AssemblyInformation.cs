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
    public class AssemblyInformation : MarshalByRefObject
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

        public string Name { get; }
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
    }
}
