using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Extensions;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Microsoft.Extensions;
using Dependencies.Analyser.Native;

namespace Dependencies.Analyser.Microsoft
{
    public class ReflectionAnalyser : BaseAnalyser
    {
        private readonly INativeAnalyser nativeAnalyser;

        private readonly IDictionary<string, IList<string>> dllImportReferences;

        public ReflectionAnalyser(IAnalyserSettingProvider settings)
            : base(settings)
        {
            nativeAnalyser = new NativeAnalyser(settings, AssembliesLoaded, LinksLoaded);

            dllImportReferences = new Dictionary<string, IList<string>>();
        }

        protected override (AssemblyInformation assembly, IDictionary<string, AssemblyLink> links) Analyse(string dllPath)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(dllPath);

                var assembly = LoadManagedAssembly(dllPath);

                if(directoryPath != null)
                    AnalyseNativeAssemblies(assembly, directoryPath);

                return (assembly, LinksLoaded);

            }
            catch (BadImageFormatException)
            {
                return (nativeAnalyser.LoadNativeAssembly(dllPath), LinksLoaded);
            }
        }

        private void AnalyseNativeAssemblies(AssemblyInformation info, string baseDirectory)
        {
            LoadNativeReferences(info, baseDirectory);

            var subAssemblies = LinksLoaded.Select(x => x.Value.Assembly).Distinct().Where(x => x.IsResolved && !string.IsNullOrEmpty(x.FilePath)).ToArray();

            foreach (var assembly in subAssemblies)
                LoadNativeReferences(assembly, baseDirectory);
        }

        private void LoadNativeReferences(AssemblyInformation assembly, string baseDirectory)
        {
            if (!assembly.IsILOnly && Settings.GetSetting<bool>(SettingKeys.ScanCliReferences) && assembly.FilePath != null)
                assembly.Links.AddRange(nativeAnalyser.GetNativeLinks(assembly.FilePath, baseDirectory));


            if (dllImportReferences.TryGetValue(assembly.FullName, out var references))
                LoadDllImportRefrences(assembly, baseDirectory, references);
        }

        private void LoadDllImportRefrences(AssemblyInformation assembly, string baseDirectory, IList<string> references)
        {
            foreach (var item in references)
            {
                var link = nativeAnalyser.GetNativeLink(item, baseDirectory);

                if (!assembly.Links.Contains(link))
                    assembly.Links.Add(link);
            }
        }

        private AssemblyInformation LoadManagedAssembly(string entryDll)
        {

            var fileInfo = new FileInfo(entryDll);
            var directoryPath = Path.GetDirectoryName(entryDll);

            if (directoryPath == null)
                throw new FileNotFoundException(entryDll);

            var runtimeAssemblies = (AppDomain.CurrentDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?.Split(Path.PathSeparator);
            var resolver = new PathAssemblyResolver(runtimeAssemblies);

            using var context = new MetadataLoadContext(resolver);

            var assembly = context.LoadFromAssemblyPath(entryDll);

            return GetManaged(context, assembly.GetName(), directoryPath, fileInfo.Extension.Replace(".", "", StringComparison.InvariantCulture));
        }


        private AssemblyInformation GetManaged(MetadataLoadContext context, AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            if (assemblyName.Name == null)
                throw new Exception($"No name for assembly {assemblyName.FullName}");

            if (AssembliesLoaded.TryGetValue(assemblyName.Name, out var assemblyFound))
                return assemblyFound;

            var (info, assembly) = CreateManagedAssemblyInformation(context, assemblyName, baseDirectory, extension);

            AssembliesLoaded.Add(assemblyName.Name, info);

            if (assembly != null && (info.IsLocalAssembly || Settings.GetSetting<bool>(SettingKeys.ScanGlobalManaged)))
            {
                info.Links.AddRange(assembly.GetReferencedAssemblies().Select(x => GetAssemblyLink(context, x, assemblyName.FullName, baseDirectory)));

                if (Settings.GetSetting<bool>(SettingKeys.ScanDllImport))
                    dllImportReferences[info.FullName] = assembly.GetDllImportReferences().ToList();
            }

            return info;
        }

        public AssemblyLink GetAssemblyLink(MetadataLoadContext context, AssemblyName assembly, string parentName,  string baseDirectory)
        {
            if (LinksLoaded.TryGetValue(assembly.FullName, out var assemblyLink))
            {
                assemblyLink.Assembly?.ParentLinkName.Add(parentName);
                return assemblyLink;
            }

            var newAssemblyLink = new AssemblyLink(assembly.Version?.ToString(), assembly.FullName);

            LinksLoaded.Add(assembly.FullName, newAssemblyLink);

            newAssemblyLink.Assembly = GetManaged(context, assembly, baseDirectory);
            newAssemblyLink.Assembly.ParentLinkName.Add(parentName);

            return newAssemblyLink;
        }

        private static (AssemblyInformation info, Assembly? assembly) CreateManagedAssemblyInformation(MetadataLoadContext context, AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            var assemblyPath = FilePathProvider.GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);

            Assembly? assembly = null;
            try
            {
                assembly = File.Exists(assemblyPath) ? context.LoadFromAssemblyPath(assemblyPath) : context.LoadFromAssemblyName(assemblyName);
            }
            catch
            {
                // In this case, assembly is not found
            }

            var assemblyShortName = assemblyName.Name ?? string.Empty;
            var assemblyVersion = assemblyName.Version?.ToString() ?? string.Empty;

            var info = new AssemblyInformation(assemblyShortName, assembly?.GetName().Version?.ToString() ?? assemblyVersion, assemblyPath)
            {
                IsLocalAssembly = assemblyPath != null || assembly == null,
                AssemblyName = assemblyName.FullName,
                IsResolved = assembly != null
            };

            info.EnhancePropertiesWithFile();
            info.EnhanceProperties(assembly?.GetModules().First());

            return (info, assembly);
        }

    }
}

