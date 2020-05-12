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


        internal static void EnhanceProperties(this AssemblyInformation info, AssemblyDefinition assembly)
        {
            if (assembly == null)
                return;

            try
            {
                info.IsILOnly = (assembly.MainModule.Attributes & ModuleAttributes.ILOnly) == ModuleAttributes.ILOnly;

                if (assembly.MainModule.Architecture == TargetArchitecture.I386 && info.IsILOnly) // This configuration is for any CPU...
                    info.TargetProcessor = TargetProcessor.AnyCpu;
                else
                    info.TargetProcessor = TargetProcessorProvider[assembly.MainModule.Architecture];

                info.IsDebug = assembly.GetIsDebugFlag();
                info.TargetFramework = assembly.GetTargetFramework();
            }
            catch
            {
                // We keep information and skip error
            }

        }

        internal static bool? GetIsDebugFlag(this AssemblyDefinition assembly)
        {
            var debugAttribute = assembly.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == "System.Diagnostics.DebuggableAttribute");

            if (debugAttribute == null) return null;

            var isDebug = false;

            if (debugAttribute.ConstructorArguments.Count == 1)
            {
                var mode = (DebuggableAttribute.DebuggingModes)debugAttribute.ConstructorArguments[0].Value;

                isDebug = (mode & DebuggableAttribute.DebuggingModes.Default) == DebuggableAttribute.DebuggingModes.Default;
            }
            else
            {
                isDebug = (bool)debugAttribute.ConstructorArguments[0].Value;
            }

            if (debugAttribute.Properties.Any(x => x.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled), StringComparison.InvariantCulture)))
            {
                var arg = debugAttribute.Properties.SingleOrDefault(x => x.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled), StringComparison.InvariantCulture));
                isDebug = !((bool)arg.Argument.Value);
            }

            return isDebug;
        }

        internal static string GetTargetFramework(this AssemblyDefinition assembly)
        {
            var targetFrameworkAttribute = assembly.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute");

            if (targetFrameworkAttribute == null || targetFrameworkAttribute.ConstructorArguments.Count != 1) return string.Empty;

            return (string)targetFrameworkAttribute.ConstructorArguments[0].Value;
        }

        internal static IEnumerable<string> GetDllImportValues(this AssemblyDefinition assembly)
        {
            var externalLibNames = assembly.Modules.SelectMany(x => x.Types)
                                          .SelectMany(x => x.Methods)
                                          .Where(x => x.IsStatic && x.IsPInvokeImpl && x.IsPreserveSig)
                                          .Select(x => x.PInvokeInfo?.Module?.Name)
                                          .Where(x => !string.IsNullOrEmpty(x))
                                          .Distinct();

            return externalLibNames;
        }
    }
}
