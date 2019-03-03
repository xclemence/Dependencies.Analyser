using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Microsoft.Extensions
{
    internal static class AssemblyInformationExtensions
    {
        private static readonly IReadOnlyDictionary<ImageFileMachine, TargetProcessor> TargetProcessorProvider = new Dictionary<ImageFileMachine, TargetProcessor>
        {
            [ImageFileMachine.I386] = TargetProcessor.x86,
            [ImageFileMachine.IA64] = TargetProcessor.x64,
            [ImageFileMachine.AMD64] = TargetProcessor.x64,
        };

        public static void EnhanceProperties(this AssemblyInformation info, Module refModule = null)
        {
            if (!info.IsLocalAssembly || !info.IsResolved)
                return;

            var fileInfo = new FileInfo(info.FilePath);
            info.CreationDate = fileInfo.CreationTime;

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(info.FilePath);
            info.Creator = fileVersionInfo.CompanyName;

            if (string.IsNullOrEmpty(info.LoadedVersion))
                info.LoadedVersion = fileVersionInfo.ProductVersion;

            info.IsDebug = fileVersionInfo.IsDebug;

            if (refModule != null)
            {
                refModule.GetPEKind(out PortableExecutableKinds kind, out ImageFileMachine machine);

                if (machine == ImageFileMachine.I386 && kind == PortableExecutableKinds.ILOnly) // This configuration is for any CPU...
                    info.TargetProcessor = TargetProcessor.AnyCpu;
                else
                    info.TargetProcessor = TargetProcessorProvider[machine];

                info.IsILOnly = (kind & PortableExecutableKinds.ILOnly) == PortableExecutableKinds.ILOnly;

                info.SetIsDebugFlag(refModule.Assembly);
            }
        }

        public static void SetIsDebugFlag(this AssemblyInformation info, Assembly assembly)
        {
            var debugAttribute = assembly.GetCustomAttributesData().SingleOrDefault(x => x.ToString().StartsWith("[System.Diagnostics.DebuggableAttribute"));

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

            if (debugAttribute.NamedArguments.Any(x => x.MemberInfo.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled))))
            {
                var arg = debugAttribute.NamedArguments.SingleOrDefault(x => x.MemberInfo.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled)));
                info.IsDebug = !((bool)arg.TypedValue.Value);
            }
        }

        public static string GetDllImportDllName(this MethodInfo method)
        {
            var attribute = method.GetCustomAttributesData().FirstOrDefault(x => x.ToString().StartsWith("[System.Runtime.InteropServices.DllImportAttribute"));

            if (attribute == null)
                return null;

            return attribute.ConstructorArguments[0].Value.ToString();
        }

        public static IEnumerable<string> GetDllImportReferences(this Assembly assembly)
        {
            var result = assembly.GetTypes().SelectMany(x => x.GetMethods())
                                            .Where(x => x.IsStatic)
                                            .Select(x => x.GetDllImportDllName())
                                            .Where(x => !string.IsNullOrEmpty(x))
                                            .Distinct()
                                            .ToList();

            return result;
        }
    }
}
