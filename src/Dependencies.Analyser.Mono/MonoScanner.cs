using System.Collections.Generic;
using System.Linq;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Extensions;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Mono.Extensions;
using Mono.Cecil;

namespace Dependencies.Analyser.Mono
{
    public class MonoScanner : IScannerPlugin
    {
        private IDictionary<string, AssemblyDefinition> cache = new Dictionary<string, AssemblyDefinition>();


        public AssemblyInformation GetAssembly(string path)
        {
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path);

            cache[assembly.FullName] = assembly;

            var info = new AssemblyInformation(assembly.Name.Name, assembly?.Name.Version.ToString(), path)
            {
                IsLocalAssembly = true,
                AssemblyName = assembly.FullName,
                IsResolved = true,
                HasEntryPoint = assembly.EntryPoint != null

            };

            info.EnhancePropertiesWithFile();
            info.EnhanceProperties(assembly);

            return info;
        }

        public AssemblyInformation GetAssembly(AssemblyName assemblyName)
        {
            AssemblyDefinition? assembly = null;
            try
            {
                using var resolver = new DefaultAssemblyResolver();
                assembly = resolver.Resolve(AssemblyNameReference.Parse(assemblyName.FullName));

                cache[assembly.FullName] = assembly;
            }
            catch
            {
                // do nothing, assembly is not found
            }

            var info = new AssemblyInformation(assemblyName.Name, assembly?.Name.Version.ToString() ?? assemblyName.Version, string.Empty)
            {
                IsLocalAssembly = assembly == null,
                AssemblyName = assembly?.FullName ?? assemblyName.FullName,
                IsResolved = assembly != null,
                HasEntryPoint = assembly?.EntryPoint != null

            };

            info.EnhancePropertiesWithFile();
            info.EnhanceProperties(assembly);

            return info;
        }

        public AssemblyName GetAssemblyName(string path)
        {
            var definition = AssemblyDefinition.ReadAssembly(path);
            return new AssemblyName(definition.FullName, definition.Name.Name, definition.Name.Version.ToString());
        }

        public IEnumerable<AssemblyName> GetReferences(AssemblyInformation assembly)
        {
            var definition = cache[assembly.FullName];

            return definition.MainModule.AssemblyReferences.Select(x => new AssemblyName(x.FullName, x.Name, x.Version.ToString()));
        }
    }
}
