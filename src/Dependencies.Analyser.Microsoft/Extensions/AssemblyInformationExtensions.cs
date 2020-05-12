using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
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

        public static void EnhanceProperties(this AssemblyInformation info, Module? refModule = null)
        {
            if (!info.IsLocalAssembly || !info.IsResolved)
                return;

            try
            {
                var fileInfo = new FileInfo(info.FilePath);
                info.CreationDate = fileInfo.CreationTime;

                var fileVersionInfo = FileVersionInfo.GetVersionInfo(info.FilePath);
                info.Creator = fileVersionInfo.CompanyName;

                if (string.IsNullOrEmpty(info.LoadedVersion))
                    info.LoadedVersion = fileVersionInfo.ProductVersion;

                info.IsDebug = fileVersionInfo.IsDebug;

                if (refModule != null)
                {
                    refModule.GetPEKind(out var kind, out var machine);

                    if (machine == ImageFileMachine.I386 && kind == PortableExecutableKinds.ILOnly) // This configuration is for any CPU...
                        info.TargetProcessor = TargetProcessor.AnyCpu;
                    else
                        info.TargetProcessor = TargetProcessorProvider[machine];

                    info.IsILOnly = (kind & PortableExecutableKinds.ILOnly) == PortableExecutableKinds.ILOnly;

                    info.IsDebug = refModule.Assembly.GetIsDebugFlag();

                    info.TargetFramework = refModule.Assembly.GetTargetFramework();

                    info.HasEntryPoint = refModule.Assembly.EntryPoint != null;
                }
            }
            catch
            {
                // Do noting, leave properties found
            }

        }

        public static bool? GetIsDebugFlag(this Assembly assembly)
        {
            var debugAttribute = assembly.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == typeof(DebuggableAttribute).FullName);

            if (debugAttribute == null) return null;

            var isDebug = false;

            if (debugAttribute.ConstructorArguments.Count == 1)
            {
                var mode = debugAttribute.ConstructorArguments[0].Value as DebuggableAttribute.DebuggingModes?;

                isDebug = (mode & DebuggableAttribute.DebuggingModes.Default) == DebuggableAttribute.DebuggingModes.Default;
            }
            else
            {
                isDebug = ((bool?)debugAttribute.ConstructorArguments[0].Value) ?? false;
            }

            if (debugAttribute.NamedArguments.Any(x => x.MemberInfo.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled), StringComparison.InvariantCulture)))
            {
                var arg = debugAttribute.NamedArguments.SingleOrDefault(x => x.MemberInfo.Name.Equals(nameof(DebuggableAttribute.IsJITTrackingEnabled), StringComparison.InvariantCulture));
                isDebug = !(((bool?)arg.TypedValue.Value ?? true));
            }

            return isDebug;
        }

        public static string GetTargetFramework(this Assembly assembly)
        {
            var targetFrameworkAttribute = assembly.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == typeof(TargetFrameworkAttribute).FullName);

            if (targetFrameworkAttribute == null || targetFrameworkAttribute.ConstructorArguments.Count != 1) return string.Empty;

            return targetFrameworkAttribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
        }

        public static string? GetDllImportDllName(this MethodInfo method)
        {
            var attribute = method.GetCustomAttributesData().FirstOrDefault(x => x.ToString().StartsWith("[System.Runtime.InteropServices.DllImportAttribute", StringComparison.InvariantCulture));

            if (attribute == null)
                return null;

            return attribute.ConstructorArguments[0].Value?.ToString();
        }

        public static IEnumerable<string> GetDllImportReferences(this Assembly assembly)
        {
            var result = assembly.GetTypes().SelectMany(x => x.GetMethods())
                                            .Where(x => x.IsStatic)
                                            .Select(x => x.GetDllImportDllName())
                                            .OfType<string>()
                                            .Distinct()
                                            .ToList();

            return result;
        }
    }
}
