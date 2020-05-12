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

        public MonoAnalyser(INativeAnalyser nativeAnalyser, ISettingProvider settings)
        {
            this.nativeAnalyser = nativeAnalyser;
            this.settings = settings;
        }

        public async Task<AssemblyInformation> AnalyseAsync(string dllPath) =>
            await Task.Run(() => LoadAssembly(dllPath)).ConfigureAwait(false);

        private AssemblyInformation LoadAssembly(string dllPath)
        {
            var assembly = LoadManagedAssembly(dllPath) ?? nativeAnalyser.LoadNativeAssembly(dllPath);
            return assembly.RemoveChildrenLoop();
        }

        public AssemblyInformation? LoadManagedAssembly(string entryDll)
        {
            try
            {
                var fileInfo = new FileInfo(entryDll);
                var baseDirectory = Path.GetDirectoryName(entryDll);

                if (baseDirectory == null)
                    return null;

                var assembly = AssemblyDefinition.ReadAssembly(entryDll);

                return GetManaged(assembly.Name, baseDirectory, fileInfo.Extension.Replace(".", "", StringComparison.InvariantCulture));
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }

        private AssemblyInformation GetManaged(AssemblyNameReference assemblyDefinition, string baseDirectory, string extension = "dll")
        {
            if (assembliesLoaded.TryGetValue(assemblyDefinition.Name, out var assemblyFound))
                return assemblyFound;

            var (info, assembly) = CreateManagedAssemblyInformation(assemblyDefinition, baseDirectory, extension);

            assembliesLoaded.Add(assemblyDefinition.Name, info);

            if (assembly != null && (info.IsLocalAssembly || settings.GetSetting<bool>(SettingKeys.ScanGlobalManaged)))
            {
                info.Links.AddRange(assembly.MainModule.AssemblyReferences.Select(x => new AssemblyLink(GetManaged(x, baseDirectory), x.Version.ToString(), x.FullName)));

                if (!info.IsILOnly && settings.GetSetting<bool>(SettingKeys.ScanCliReferences) && info.FilePath != null)
                    info.Links.AddRange(nativeAnalyser.GetNativeLinks(info.FilePath, baseDirectory));

                if (settings.GetSetting<bool>(SettingKeys.ScanDllImport))
                    AppendDllImportDll(info, assembly, baseDirectory);
            }

            return info;
        }

        private void AppendDllImportDll(AssemblyInformation info, AssemblyDefinition assembly, string baseDirectory)
        {
            var externalDllNames = assembly.GetDllImportValues();

            foreach (var item in externalDllNames)
            {
                var link = nativeAnalyser.GetNativeLink(item, baseDirectory);

                if (!info.Links.Contains(link))
                    info.Links.Add(link);
            }
        }

        private static (AssemblyInformation info, AssemblyDefinition? assembly) CreateManagedAssemblyInformation(AssemblyNameReference assemblyName, string? baseDirectory, string extension = "dll")
        {
            var assemblyPath = FilePathProvider.GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);

            AssemblyDefinition? assembly = null;
            try
            {
                using var resolver = new DefaultAssemblyResolver();
                assembly = assemblyPath != null ? AssemblyDefinition.ReadAssembly(assemblyPath) : resolver.Resolve(assemblyName); ;
            }
            catch
            {
                // do nothing, assembly is not found
            }

            var info = new AssemblyInformation(assemblyName.Name, assembly?.Name.Version.ToString() ?? assemblyName.Version.ToString(), assemblyPath)
            {
                IsLocalAssembly = assemblyPath != null || assembly == null,
                AssemblyName = assembly?.FullName ?? assemblyName.FullName,
                IsResolved = assembly != null,
                HasEntryPoint = assembly?.EntryPoint != null

            };

            info.EnhancePropertiesWithFile();
            info.EnhanceProperties(assembly);

            return (info, assembly);
        }
    }
}
