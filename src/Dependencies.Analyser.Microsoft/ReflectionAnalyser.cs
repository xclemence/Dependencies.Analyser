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

                var entryAssembly = proxy.LoadAssembly(dllPath, settings.GetSettring<bool>(SettingKeys.ScanGlobalManaged));

                var result = entryAssembly.DeepCopy();

                var baseDirectory = Path.GetDirectoryName(dllPath);
                AnalyseNativeAssemblies(result, baseDirectory);

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
        private string dllPath;
        private bool loadGlobal;

        public ManagedAnalyserIsolation()
        {
            assembliesLoaded = new Dictionary<string, AssemblyInformation>();
        }

        private AssemblyInformation EntryAssembly { get; set; }

        public AssemblyInformation LoadAssembly(string entryDll, bool loadGlobalAssemblies)
        {
            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            dllPath = baseDirectory;
            loadGlobal = loadGlobalAssemblies;

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnCurrentDomainReflectionOnlyAssemblyResolve;

            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(entryDll);

            EntryAssembly = GetManaged(assembly.GetName(), baseDirectory, fileInfo.Extension.Replace(".", ""));

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= OnCurrentDomainReflectionOnlyAssemblyResolve;

            return EntryAssembly;
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
                info.Links.AddRange(assembly.GetReferencedAssemblies().Select(x => new AssemblyLink(GetManaged(x, baseDirectory), x.Version.ToString())));

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

            Assembly assembly = null;
            try
            {
                assembly = asmToCheck != null ? Assembly.ReflectionOnlyLoadFrom(asmToCheck) : Assembly.ReflectionOnlyLoad(assemblyName.FullName);
            }
            catch (Exception ex)
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

