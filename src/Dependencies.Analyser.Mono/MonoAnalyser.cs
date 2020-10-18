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

                return GetManaged(assembly.Name, baseDirectory, fileInfo.Extension.Replace(".", "", StringComparison.InvariantCulture));
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }

        private AssemblyInformation GetManaged(AssemblyNameReference assemblyDefinition, string baseDirectory, string extension = "dll")
        {
            if (AssembliesLoaded.TryGetValue(assemblyDefinition.Name, out var assemblyFound))
                return assemblyFound;

            var (info, assembly) = CreateManagedAssemblyInformation(assemblyDefinition, baseDirectory, extension);

            Console.WriteLine($"WARNING: {assemblyDefinition.Name}");

            AssembliesLoaded.Add(assemblyDefinition.Name, info);

            if (assembly != null && (info.IsLocalAssembly || Settings.GetSetting<bool>(SettingKeys.ScanGlobalManaged)))
            {
                info.Links.AddRange(assembly.MainModule.AssemblyReferences.Select(x => GetAssemblyLink(x, baseDirectory)));

                if (!info.IsILOnly && Settings.GetSetting<bool>(SettingKeys.ScanCliReferences) && info.FilePath != null)
                    info.Links.AddRange(NativeAnalyser.GetNativeLinks(info.FilePath, baseDirectory));

                if (Settings.GetSetting<bool>(SettingKeys.ScanDllImport))
                    AppendDllImportDll(info, assembly, baseDirectory);
            }

            return info;
        }


        public AssemblyLink GetAssemblyLink(AssemblyNameReference assembly, string baseDirectory)
        {
            if (LinksLoaded.TryGetValue(assembly.FullName, out var assemblyLink))
                return assemblyLink;

            var newAssemblyLink = new AssemblyLink(GetManaged(assembly, baseDirectory), assembly.Version.ToString(), assembly.FullName);

            LinksLoaded.Add(assembly.FullName, newAssemblyLink);

            return newAssemblyLink;
        }

        private void AppendDllImportDll(AssemblyInformation info, AssemblyDefinition assembly, string baseDirectory)
        {
            var externalDllNames = assembly.GetDllImportValues();

            foreach (var item in externalDllNames)
            {
                var link = NativeAnalyser.GetNativeLink(item, baseDirectory);

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
