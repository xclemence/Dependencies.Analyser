using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Extensions;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Mono.Extensions;
using Dependencies.Analyser.Native;
using Mono.Cecil;

namespace Dependencies.Analyser.Mono
{
    public class MonoAnalyser : BaseAnalyser
    {
        public MonoAnalyser(IAnalyserSettingProvider settings) :
            base(settings)
        {
            NativeAnalyser = new NativeAnalyser(settings, AssembliesLoaded, LinksLoaded);
        }

        protected INativeAnalyser NativeAnalyser { get; }

        protected override (AssemblyInformation assembly, IDictionary<string, AssemblyLink> links) Analyse(string dllPath)
        {
            var assembly = LoadManagedAssembly(dllPath) ?? NativeAnalyser.LoadNativeAssembly(dllPath);

            return (assembly, LinksLoaded);
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

                return GetRootManaged(assembly.Name, baseDirectory, fileInfo.Extension.Replace(".", "", StringComparison.InvariantCulture));
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }
        private AssemblyInformation GetRootManaged(AssemblyNameReference assemblyDefinition, string baseDirectory, string extension)
        {
            var (assembly, monoAssembly) = GetManaged(assemblyDefinition, baseDirectory, extension);

            AddLoadedAssemblies(assembly, monoAssembly, assemblyDefinition.FullName, baseDirectory);

            return assembly;
        }


        private (AssemblyInformation assembly, AssemblyDefinition? monoAssembly) GetManaged(AssemblyNameReference assemblyDefinition, string baseDirectory, string extension = "dll")
        {
            if (AssembliesLoaded.TryGetValue(assemblyDefinition.Name, out var assemblyFound))
                return (assemblyFound, null);

            var assemblyInfos = CreateManagedAssemblyInformation(assemblyDefinition, baseDirectory, extension);

            AssembliesLoaded.Add(assemblyDefinition.Name, assemblyInfos.assembly);

            return assemblyInfos;
        }

        private void AddLoadedAssemblies(AssemblyInformation assembly, AssemblyDefinition? monoAssembly, string parentAssemblyName, string baseDirectory)
        {
            if (monoAssembly != null && (assembly.IsLocalAssembly || Settings.GetSetting<bool>(SettingKeys.ScanGlobalManaged)))
            {
                assembly.Links.AddRange(monoAssembly.MainModule.AssemblyReferences.Select(x => GetAssemblyLink(x, parentAssemblyName, baseDirectory)));

                if (!assembly.IsILOnly && Settings.GetSetting<bool>(SettingKeys.ScanCliReferences) && assembly.FilePath != null)
                    assembly.Links.AddRange(NativeAnalyser.GetNativeLinks(assembly, parentAssemblyName, baseDirectory));

                if (Settings.GetSetting<bool>(SettingKeys.ScanDllImport))
                    AppendDllImportDll(assembly, monoAssembly, parentAssemblyName, baseDirectory);
            }
        }

        public AssemblyLink GetAssemblyLink(AssemblyNameReference assemblNamey, string parentName, string baseDirectory)
        {
            if (LinksLoaded.TryGetValue(assemblNamey.FullName, out var assemblyLink))
            {
                assemblyLink.Assembly?.ParentLinkName.Add(parentName);
                return assemblyLink;
            }

            var (assembly, monoAssembly) =  GetManaged(assemblNamey, baseDirectory);

            var newAssemblyLink = new AssemblyLink(assembly, assemblNamey.Version.ToString(), assemblNamey.FullName);

            LinksLoaded.Add(assemblNamey.FullName, newAssemblyLink);
            
            AddLoadedAssemblies(assembly, monoAssembly, assemblNamey.FullName, baseDirectory);
            newAssemblyLink.Assembly.ParentLinkName.Add(parentName);

            return newAssemblyLink;
        }

        private void AppendDllImportDll(AssemblyInformation info, AssemblyDefinition assembly, string parentName, string baseDirectory)
        {
            var externalDllNames = assembly.GetDllImportValues();

            foreach (var item in externalDllNames)
            {
                var link = NativeAnalyser.GetNativeLink(item, baseDirectory, parentName);

                if (!info.Links.Contains(link))
                    info.Links.Add(link);
            }
        }

        private static (AssemblyInformation assembly, AssemblyDefinition? monoAssembly) CreateManagedAssemblyInformation(AssemblyNameReference assemblyName, string? baseDirectory, string extension = "dll")
        {
            var assemblyPath = FilePathProvider.GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);

            AssemblyDefinition? assembly = null;
            try
            {
                using var resolver = new DefaultAssemblyResolver();
                assembly = assemblyPath != null ? AssemblyDefinition.ReadAssembly(assemblyPath) : resolver.Resolve(assemblyName);
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
