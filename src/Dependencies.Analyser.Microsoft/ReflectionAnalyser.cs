using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Microsoft.Extensions;

namespace Dependencies.Analyser.Microsoft
{
    //public class ReflectionAnalyser : MarshalByRefObject, IAssemblyAnalyser
    //{
    //    private readonly string assemblyFullPath;
    //    private readonly IServiceFactory<INativeAnalyser> nativeAnalyserFactory;
    //    private string assemblyRelativePath;

    //    public ReflectionAnalyser(IServiceFactory<INativeAnalyser> nativeAnalyserFactory)
    //    {
    //        var assemblyPath = typeof(ReflectionAnalyser).Assembly.Location;

    //        var directory = Path.GetDirectoryName(assemblyPath);

    //        assemblyFullPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
    //        assemblyRelativePath = directory.Replace(assemblyFullPath, ".");
    //        this.nativeAnalyserFactory = nativeAnalyserFactory;
    //    }

    //    public async Task<AssemblyInformation> AnalyseAsync(string dllPath)
    //    {
    //        return await Task.Run(() =>
    //        {
    //            try
    //            {
    //                var domainSetup = new AppDomainSetup
    //                {
    //                    PrivateBinPath = assemblyRelativePath
    //                };

    //                var domain = AppDomain.CreateDomain("MainResolveDomain", null, domainSetup);

    //                //AppDomain.CurrentDomain.AssemblyResolve += OnCurrentDomainAssemblyResolve; ;
    //                //Type type = typeof(ManagedAnalyserIsolation);
    //                //var proxy = (ManagedAnalyserIsolation)domain.CreateInstanceAndUnwrap(
    //                //    type.Assembly.FullName,
    //                //    type.FullName);

    //                var item = new ManagedAnalyserIsolation();

    //                var entryAssembly = item.LoadAssembly(dllPath, nativeAnalyserFactory);

    //                var result = entryAssembly.DeepCopy();

    //                AppDomain.Unload(domain);

    //                return result;
    //            }
    //            catch (Exception ex)
    //            {
    //                return null;
    //            }
    //            finally
    //            {
    //                AppDomain.CurrentDomain.AssemblyResolve -= OnCurrentDomainAssemblyResolve; ;
    //            }

    //        }).ConfigureAwait(false);
    //    }

    //    private Assembly OnCurrentDomainAssemblyResolve(object sender, ResolveEventArgs args)
    //    {

    //        var analyseAssembly = typeof(ReflectionAnalyser).Assembly;
    //        if (args.Name == analyseAssembly.FullName)
    //            return analyseAssembly;

    //        return Assembly.Load(args.Name);
    //    }
    //}

    public class ReflectionAnalyser : IAssemblyAnalyser
    {
        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded;
        private INativeAnalyser nativeAnalyser;
        private string dllPath;

        public ReflectionAnalyser(INativeAnalyser nativeAnalyser)
        {
            this.nativeAnalyser = nativeAnalyser;
            assembliesLoaded = new Dictionary<string, AssemblyInformation>();
        }

        private AssemblyInformation EntryAssembly { get; set; }


        public async Task<AssemblyInformation> AnalyseAsync(string entryDll)
        {
            return await Task.Run(() => LoadAssembly(entryDll) ?? nativeAnalyser.LoadNativeAssembly(dllPath)).ConfigureAwait(false);
        }

        public AssemblyInformation LoadAssembly(string entryDll)
        {

            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            dllPath = baseDirectory;

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

            if (assembly != null && info.IsLocalAssembly)
            {
                info.Links.AddRange(assembly.GetReferencedAssemblies().Select(x => new AssemblyLink(GetManaged(x, baseDirectory), x.Version.ToString())));

                if (!info.IsILOnly)
                    info.Links.AddRange(nativeAnalyser.GetNativeLinks(info.FilePath, baseDirectory));
            }

            return info;
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
                // do norting
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

