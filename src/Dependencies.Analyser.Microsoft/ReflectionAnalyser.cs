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
            LoadNativeReferences(info, info.FullName, baseDirectory);

            var subAssemblies = LinksLoaded.Select(x => x.Value).Where(x => x.Assembly.IsResolved && !string.IsNullOrEmpty(x.Assembly.FilePath)).ToArray();

            foreach (var link in subAssemblies)
                LoadNativeReferences(link.Assembly, link.LinkFullName, baseDirectory);
        }

        private void LoadNativeReferences(AssemblyInformation assembly, string parentName, string baseDirectory)
        {
            if (!assembly.IsILOnly && Settings.GetSetting<bool>(SettingKeys.ScanCliReferences) && assembly.FilePath != null)
                assembly.Links.AddRange(nativeAnalyser.GetNativeLinks(assembly, parentName, baseDirectory));


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

            var runtimeAssemblies = (AppDomain.CurrentDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?.Split(Path.PathSeparator) ?? new string[0];
            var resolver = new PathAssemblyResolver(runtimeAssemblies);

            using var context = new MetadataLoadContext(resolver);

            var assembly = context.LoadFromAssemblyPath(entryDll);

            return GetRootManaged(context, assembly.GetName(), directoryPath, fileInfo.Extension.Replace(".", "", StringComparison.InvariantCulture));
        }

        private AssemblyInformation GetRootManaged(MetadataLoadContext context, System.Reflection.AssemblyName assemblyName, string baseDirectory, string extension)
        {
            var (assembly, msAssembly) = GetManaged(context, assemblyName, baseDirectory, extension);

            AddLoadedAssemblies(assembly, msAssembly, context, assembly.FullName, baseDirectory);

            return assembly;
        }

        private (AssemblyInformation assembly, Assembly? msAssembly) GetManaged(MetadataLoadContext context, System.Reflection.AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            if (assemblyName.Name == null)
                throw new ArgumentNullException($"No name for assembly {assemblyName.FullName}");

            if (AssembliesLoaded.TryGetValue(assemblyName.Name, out var assemblyFound))
                return (assemblyFound, null);

            var (assembly, msAssembly) = CreateManagedAssemblyInformation(context, assemblyName, baseDirectory, extension);

            AssembliesLoaded.Add(assemblyName.Name, assembly);

            return (assembly, msAssembly);
        }

        private void AddLoadedAssemblies(AssemblyInformation assembly, Assembly? msAssembly, MetadataLoadContext context, string parentAssemblyName, string baseDirectory)
        {
            if (msAssembly != null && (assembly.IsLocalAssembly || Settings.GetSetting<bool>(SettingKeys.ScanGlobalManaged)))
            {
                assembly.Links.AddRange(msAssembly.GetReferencedAssemblies().Select(x => GetAssemblyLink(context, x, parentAssemblyName, baseDirectory)));

                if (Settings.GetSetting<bool>(SettingKeys.ScanDllImport))
                    dllImportReferences[assembly.FullName] = msAssembly.GetDllImportReferences().ToList();
            }
        }

        public AssemblyLink GetAssemblyLink(MetadataLoadContext context, System.Reflection.AssemblyName assemblyName, string parentName,  string baseDirectory)
        {
            if (LinksLoaded.TryGetValue(assemblyName.FullName, out var assemblyLink))
            {
                assemblyLink.Assembly?.ParentLinkName.Add(parentName);
                return assemblyLink;
            }

            var (assembly, msAssembly) = GetManaged(context, assemblyName, baseDirectory);

            var newAssemblyLink = new AssemblyLink(assembly, assemblyName.Version?.ToString(), assemblyName.FullName);
            LinksLoaded.Add(assemblyName.FullName, newAssemblyLink);

            AddLoadedAssemblies(assembly, msAssembly, context, assemblyName.FullName, baseDirectory);

            newAssemblyLink.Assembly.ParentLinkName.Add(parentName);

            return newAssemblyLink;
        }

        private static (AssemblyInformation assembly, Assembly? msAssembly) CreateManagedAssemblyInformation(MetadataLoadContext context, System.Reflection.AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            var assemblyPath = FilePathProvider.GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);

            Assembly? assembly = null;
            try
            {
                assembly = File.Exists(assemblyPath) ? context.LoadFromAssemblyPath(assemblyPath ?? string.Empty) : context.LoadFromAssemblyName(assemblyName);
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

