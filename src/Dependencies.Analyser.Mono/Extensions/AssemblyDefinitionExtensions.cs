using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dependencies.Analyser.Base.Models;
using Mono.Cecil;

namespace Dependencies.Analyser.Mono.Extensions
{
    internal static class AssemblyDefinitionExtensions
    {
        private static readonly IReadOnlyDictionary<TargetArchitecture, TargetProcessor> TargetProcessorProvider = new Dictionary<TargetArchitecture, TargetProcessor>
        {
            [TargetArchitecture.I386] = TargetProcessor.x86,
            [TargetArchitecture.IA64] = TargetProcessor.x64,
            [TargetArchitecture.AMD64] = TargetProcessor.x64,
        };


        public static void EnhanceProperties(this AssemblyInformation info, AssemblyDefinition assembly)
        {
            info.IsILOnly = (assembly.MainModule.Attributes & ModuleAttributes.ILOnly) == ModuleAttributes.ILOnly;

            if (assembly.MainModule.Architecture == TargetArchitecture.I386 && info.IsILOnly) // This configuration is for any CPU...
                info.TargetProcessor = TargetProcessor.AnyCpu;
            else
                info.TargetProcessor = TargetProcessorProvider[assembly.MainModule.Architecture];


            info.SetIsDebugFlag(assembly);
        }

        public static void SetIsDebugFlag(this AssemblyInformation info, AssemblyDefinition assembly)
        {
            var debugAttribute = assembly.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == "System.Diagnostics.DebuggableAttribute");

            if (debugAttribute == null) return;

            if (debugAttribute.ConstructorArguments.Count == 1)
            {
                var mode = (DebuggableAttribute.DebuggingModes)debugAttribute.ConstructorArguments[0].Value;

                info.IsDebug = (mode & DebuggableAttribute.DebuggingModes.Default) == DebuggableAttribute.DebuggingModes.Default;
            }
            else
            {
                info.IsDebug = (bool)debugAttribute.ConstructorArguments[0].Value;
            }

            if (debugAttribute.Properties.Any(x => x.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled))))
            {
                var arg = debugAttribute.Properties.SingleOrDefault(x => x.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled)));
                info.IsDebug = !((bool)arg.Argument.Value);
            }
        }
    }
}
