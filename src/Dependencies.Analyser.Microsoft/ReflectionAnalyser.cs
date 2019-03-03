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
    public class ReflectionAnalyser : MarshalByRefObject, IAssemblyAnalyser
    {
        private readonly string assemblyFullPath;
        private readonly INativeAnalyser nativeAnalyser;
        private readonly ISettingProvider settings;
        private readonly string assemblyRelativePath;

        public ReflectionAnalyser(INativeAnalyser nativeAnalyser, ISettingProvider settings)
        {
            var assemblyPath = typeof(ReflectionAnalyser).Assembly.Location;

            var directory = Path.GetDirectoryName(assemblyPath);

            assemblyFullPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            assemblyRelativePath = directory.Replace(assemblyFullPath, ".");
            this.nativeAnalyser = nativeAnalyser;
            this.settings = settings;
        }

        public async Task<AssemblyInformation> AnalyseAsync(string dllPath) => 
            await Task.Run(() => LoadAssembly(dllPath).RemoveChildenLoop()).ConfigureAwait(false);

        private AssemblyInformation LoadAssembly(string dllPath)
        {
            try
            {
                var domainSetup = new AppDomainSetup
                {
                    PrivateBinPath = assemblyRelativePath
                };

                var domain = AppDomain.CreateDomain("MainResolveDomain", null, domainSetup);

                AppDomain.CurrentDomain.AssemblyResolve += OnCurrentDomainAssemblyResolve; ;
                Type type = typeof(ManagedAnalyserIsolation);
                var proxy = (ManagedAnalyserIsolation)domain.CreateInstanceAndUnwrap(
                    type.Assembly.FullName,
                    type.FullName);

                var (assembly, dllImports) = proxy.LoadAssembly(dllPath, settings.GetSettring<bool>(SettingKeys.ScanGlobalManaged), settings.GetSettring<bool>(SettingKeys.ScanDllImport));

                var result = assembly.DeepCopy();

                var baseDirectory = Path.GetDirectoryName(dllPath);
                AnalyseNativeAssemblies(result, baseDirectory, dllImports);

                AppDomain.Unload(domain);

                return result;
            }
            catch (BadImageFormatException)
            {
                return nativeAnalyser.LoadNativeAssembly(dllPath);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= OnCurrentDomainAssemblyResolve;
            }
        }

        private void AnalyseNativeAssemblies(AssemblyInformation info, string baseDirectory, IDictionary<string, IList<string>> dllImports)
        {
            LoadNativeReferences(info, baseDirectory, dllImports);

            var subAssemblies = info.GetAllLinks().Select(x => x.Assembly).Distinct().Where(x => x.IsResolved && !string.IsNullOrEmpty(x.FilePath)).ToArray();

            foreach(var assembly in subAssemblies)
                LoadNativeReferences(assembly, baseDirectory, dllImports);
        }

        private void LoadNativeReferences(AssemblyInformation assembly, string baseDirectory, IDictionary<string, IList<string>> dllImports)
        {
            if (!assembly.IsILOnly && settings.GetSettring<bool>(SettingKeys.ScanCliReferences))
                assembly.Links.AddRange(nativeAnalyser.GetNativeLinks(assembly.FilePath, baseDirectory));


            if(dllImports.TryGetValue(assembly.FullName, out IList<string> references))
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

        private Assembly OnCurrentDomainAssemblyResolve(object sender, ResolveEventArgs args)
        {

            var analyseAssembly = typeof(ReflectionAnalyser).Assembly;
            if (args.Name == analyseAssembly.FullName)
                return analyseAssembly;

            return Assembly.Load(args.Name);
        }
    }

    public class ManagedAnalyserIsolation : MarshalByRefObject
    {
        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded;
        private readonly IDictionary<string, IList<string>> dllImportReferences;
        private string dllPath;
        private bool loadGlobal;
        private bool analyseDllimportAttribute;

        public ManagedAnalyserIsolation()
        {
            assembliesLoaded = new Dictionary<string, AssemblyInformation>();
            dllImportReferences = new Dictionary<string, IList<string>>();
        }

        private AssemblyInformation EntryAssembly { get; set; }

        public (AssemblyInformation assembly, IDictionary<string, IList<string>> dllImports) LoadAssembly(string entryDll, bool loadGlobalAssemblies, bool analyseDllimportAttribute)
        {
            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            dllPath = baseDirectory;
            loadGlobal = loadGlobalAssemblies;
            this.analyseDllimportAttribute = analyseDllimportAttribute;

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnCurrentDomainReflectionOnlyAssemblyResolve;

            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(entryDll);

            EntryAssembly = GetManaged(assembly.GetName(), baseDirectory, fileInfo.Extension.Replace(".", ""));

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= OnCurrentDomainReflectionOnlyAssemblyResolve;

            return (EntryAssembly, dllImportReferences);
        }

        private Assembly OnCurrentDomainReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var fileName = args.Name.Split(',').First();

            var file = Path.Combine(dllPath, $"{fileName}.dll");

            return File.Exists(file) ? Assembly.ReflectionOnlyLoadFrom(file) : Assembly.ReflectionOnlyLoad(args.Name);
        }

        public AssemblyInformation GetManaged(AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            if (assembliesLoaded.TryGetValue(assemblyName.Name, out AssemblyInformation assemblyFound))
                return assemblyFound;

            var (info, assembly) = CreateManagedAssemblyInformation(assemblyName, baseDirectory, extension);

            assembliesLoaded.Add(assemblyName.Name, info);

            if (assembly != null && (info.IsLocalAssembly || loadGlobal))
            {
                info.Links.AddRange(assembly.GetReferencedAssemblies().Select(x => new AssemblyLink(GetManaged(x, baseDirectory), x.Version.ToString())));

                if (analyseDllimportAttribute)
                    dllImportReferences[info.FullName] = assembly.GetDllImportReferences().ToList();
            }

            return info;
        }

        private Assembly SearchInLoadedAssembly(AssemblyName assemblyName)
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == assemblyName.FullName) ?? Assembly.Load(assemblyName);
            }
            catch
            {
                return null;
            }
        }

        private (AssemblyInformation info, Assembly assembly) CreateManagedAssemblyInformation(AssemblyName assemblyName, string baseDirectory, string extension = "dll")
        {
            var asmToCheck = GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);
            Assembly assembly;

            try
            {
                assembly = asmToCheck != null ? Assembly.ReflectionOnlyLoadFrom(asmToCheck) : Assembly.ReflectionOnlyLoad(assemblyName.FullName);
            }
            catch
            {
                asmToCheck = null;
                assembly = SearchInLoadedAssembly(assemblyName);
            }

            var info = new AssemblyInformation(assemblyName.Name, assembly?.GetName().Version.ToString() ?? assemblyName.Version.ToString(), asmToCheck)
            {
                IsLocalAssembly = asmToCheck != null || assembly == null,
                AssemblyName = assembly?.FullName
            };

            try
            {
                info.EnhanceProperties(assembly?.GetModules().First());
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

