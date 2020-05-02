using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Extensions;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Microsoft.Extensions;

namespace Dependencies.Analyser.Microsoft
{
    public class ReflectionAnalyser : IAssemblyAnalyser
    {
        private readonly INativeAnalyser nativeAnalyser;
        private readonly ISettingProvider settings;

        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded;
        private readonly IDictionary<string, IList<string>> dllImportReferences;
    

        public ReflectionAnalyser(INativeAnalyser nativeAnalyser, ISettingProvider settings)
        {
            this.nativeAnalyser = nativeAnalyser;
            this.settings = settings;

            assembliesLoaded = new Dictionary<string, AssemblyInformation>();
            dllImportReferences = new Dictionary<string, IList<string>>();
        }

        public async Task<AssemblyInformation> AnalyseAsync(string dllPath) => 
            await Task.Run(() => LoadAssembly(dllPath).RemoveChildenLoop()).ConfigureAwait(false);

        private AssemblyInformation LoadAssembly(string dllPath)
        {
            try
            {
                var directoryPath = Path.GetDirectoryName(dllPath);
                var baseDirectory = new DirectoryInfo(directoryPath);

                var allDirectories = baseDirectory.GetFiles("*qdqdsd.dll", SearchOption.AllDirectories).Select(x => x.FullName).ToList();

                var runtimeAssemblies = ((string)AppDomain.CurrentDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
                var resolver = new PathAssemblyResolver(runtimeAssemblies);

                using var context = new MetadataLoadContext(resolver);
                var assembly = LoadAssembly(context, dllPath);

                AnalyseNativeAssemblies(assembly, directoryPath);
                return assembly;

            }
            catch (BadImageFormatException)
            {
                return nativeAnalyser.LoadNativeAssembly(dllPath);
            }
        }

        private void AnalyseNativeAssemblies(AssemblyInformation info, string baseDirectory)
        {
            LoadNativeReferences(info, baseDirectory);

            var subAssemblies = info.GetAllLinks().Select(x => x.Assembly).Distinct().Where(x => x.IsResolved && !string.IsNullOrEmpty(x.FilePath)).ToArray();

            foreach(var assembly in subAssemblies)
                LoadNativeReferences(assembly, baseDirectory);
        }

        private void LoadNativeReferences(AssemblyInformation assembly, string baseDirectory)
        {
            if (!assembly.IsILOnly && settings.GetSettring<bool>(SettingKeys.ScanCliReferences))
                assembly.Links.AddRange(nativeAnalyser.GetNativeLinks(assembly.FilePath, baseDirectory));


            if(dllImportReferences.TryGetValue(assembly.FullName, out IList<string> references))
                LoadDllImportRefrences(assembly, baseDirectory, references);
        }

        private void LoadDllImportRefrences(AssemblyInformation assembly, string baseDirectory, IList<string> references)
        {
            foreach (var item in references)
            {
                var link = nativeAnalyser.GetNativeLink(item, baseDirectory);

                if(!assembly.Links.Contains(link))
                    assembly.Links.Add(link);
            }
        }


        public AssemblyInformation LoadAssembly(MetadataLoadContext context, string entryDll)
        {
            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            Assembly assembly = context.LoadFromAssemblyPath(entryDll);

            return GetManaged(context, assembly.GetName(), baseDirectory, fileInfo.Extension.Replace(".", ""));
        }


        public AssemblyInformation GetManaged(MetadataLoadContext context, AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            if (assembliesLoaded.TryGetValue(assemblyName.Name, out AssemblyInformation assemblyFound))
                return assemblyFound;

            var (info, assembly) = CreateManagedAssemblyInformation(context, assemblyName, baseDirectory, extension);

            assembliesLoaded.Add(assemblyName.Name, info);

            if (assembly != null && (info.IsLocalAssembly || settings.GetSettring<bool>(SettingKeys.ScanGlobalManaged)))
            {
                info.Links.AddRange(assembly.GetReferencedAssemblies().Select(x => new AssemblyLink(GetManaged(context, x, baseDirectory), x.Version.ToString())));

                if (settings.GetSettring<bool>(SettingKeys.ScanDllImport))
                    dllImportReferences[info.FullName] = assembly.GetDllImportReferences().ToList();
            }

            return info;
        }

        private (AssemblyInformation info, Assembly assembly) CreateManagedAssemblyInformation(MetadataLoadContext context, AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            var assemblyPath = GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);

            Assembly assembly = null;
            try 
            {
                assembly = File.Exists(assemblyPath) ? context.LoadFromAssemblyPath(assemblyPath) : context.LoadFromAssemblyName(assemblyName);
            }
            catch
            {
                // In this case, assembly is not found
            }

            var info = new AssemblyInformation(assemblyName.Name, assembly?.GetName().Version.ToString() ?? assemblyName.Version.ToString(), assemblyPath)
            {
                IsLocalAssembly = assemblyPath != null || assembly == null,
                AssemblyName = assemblyName.FullName,
                IsResolved = assembly != null
            };

            info.EnhancePropertiesWithFile();
            info.EnhanceProperties(assembly?.GetModules().First());

            return (info, assembly);
        }

        private string GetAssemblyPath(string fileName, string baseDirectory)
        {
            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }

    }
}

