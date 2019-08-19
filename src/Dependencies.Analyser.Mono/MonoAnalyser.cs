using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Extensions;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Mono.Extensions;
using Mono.Cecil;

namespace Dependencies.Analyser.Mono
{
    public class MonoAnalyser : IAssemblyAnalyser
    {
        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded = new Dictionary<string, AssemblyInformation>();
        private readonly INativeAnalyser nativeAnalyser;
        private readonly ISettingProvider settings;

        public MonoAnalyser(INativeAnalyser  nativeAnalyser, ISettingProvider settings)
        {
            this.nativeAnalyser = nativeAnalyser;
            this.settings = settings;
        }

        public async Task<AssemblyInformation> AnalyseAsync(string dllPath) =>
            await Task.Run(() => LoadAssembly(dllPath)).ConfigureAwait(false);

        private AssemblyInformation LoadAssembly(string dllPath)
        {
            var assembly = LoadManagedAssembly(dllPath) ?? nativeAnalyser.LoadNativeAssembly(dllPath);
            return assembly.RemoveChildenLoop();
        }

        public AssemblyInformation LoadManagedAssembly(string entryDll)
        {
            try
            {
                var fileInfo = new FileInfo(entryDll);
                var baseDirectory = Path.GetDirectoryName(entryDll);

                var assembly = AssemblyDefinition.ReadAssembly(entryDll);

                return GetManaged(assembly.Name, baseDirectory, fileInfo.Extension.Replace(".", ""));
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }

        public AssemblyInformation GetManaged(AssemblyNameReference assemblyDefinition, string baseDirectory, string extension = "dll")
        {
            if (assembliesLoaded.TryGetValue(assemblyDefinition.Name, out AssemblyInformation assemblyFound))
                return assemblyFound;

            var (info, assembly) = CreateManagedAssemblyInformation(assemblyDefinition, baseDirectory, extension);

            assembliesLoaded.Add(assemblyDefinition.Name, info);

            if (assembly != null && (info.IsLocalAssembly || settings.GetSettring<bool>(SettingKeys.ScanGlobalManaged)))
            {
                info.Links.AddRange(assembly.MainModule.AssemblyReferences.Select(x => new AssemblyLink(GetManaged(x, baseDirectory), x.Version.ToString())));

                if (!info.IsILOnly && settings.GetSettring<bool>(SettingKeys.ScanCliReferences))
                    info.Links.AddRange(nativeAnalyser.GetNativeLinks(info.FilePath, baseDirectory));

                if (settings.GetSettring<bool>(SettingKeys.ScanDllImport))
                    AppendDllImportDll(info, assembly, baseDirectory);
            }

            return info;
        }

        private void AppendDllImportDll(AssemblyInformation info, AssemblyDefinition assembly, string baseDirectory)
        {
            var externalDllNames = GetDllImportValues(assembly);

            foreach (var item in externalDllNames)
            {
                var link = nativeAnalyser.GetNativeLink(item, baseDirectory);

                if (!info.Links.Contains(link))
                    info.Links.Add(link);
            }
        }

        private IEnumerable<string> GetDllImportValues(AssemblyDefinition assembly)
        {
            var externalLibNames = assembly.Modules.SelectMany(x => x.Types)
                                          .SelectMany(x => x.Methods)
                                          .Where(x => x.IsStatic && x.IsPInvokeImpl && x.IsPreserveSig)
                                          .Select(x => x.PInvokeInfo?.Module?.Name)
                                          .Where(x => !string.IsNullOrEmpty(x))
                                          .Distinct();

            return externalLibNames;
        }

        private (AssemblyInformation info, AssemblyDefinition assembly) CreateManagedAssemblyInformation(AssemblyNameReference assemblyName, string baseDirectory, string extension = "dll")
        {
            var assemblyPath = GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);

            AssemblyDefinition assembly = null;
            try
            {
                var resolver = new DefaultAssemblyResolver();
                assembly = assemblyPath != null ? AssemblyDefinition.ReadAssembly(assemblyPath) : resolver.Resolve(assemblyName); ;
            }
            catch
            {
                // do norting
            }

            var info = new AssemblyInformation(assemblyName.Name, assembly?.Name.Version.ToString() ?? assemblyName.Version.ToString(), assemblyPath)
            {
                IsLocalAssembly = assemblyPath != null || assembly == null,
                AssemblyName = assembly?.FullName
            };

            try
            {
                info.EnhancePropertiesWithFile();

                if (assembly != null)
                    info.EnhanceProperties(assembly);
            }
            catch
            {
                // no more informations
            }

            return (info, assembly);
        }

        private string GetAssemblyPath(string fileName, string baseDirectory)
        {
            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }
    }
}
