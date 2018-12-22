using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Reflection.Extensions;

namespace Dependencies.Analyser.Reflection
{
    public class ReflexionAnalyser : MarshalByRefObject, IManagedAnalyser
    {
        public ReflexionAnalyser(string entryDll)
        {
            EntryDll = entryDll;
        }

        public string EntryDll { get; }

        public async Task<AssemblyInformation> AnalyseAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var domain = AppDomain.CreateDomain("MainResolveDomain");

                    Type type = typeof(ManagedAnalyserIsolation);
                    var proxy = (ManagedAnalyserIsolation)domain.CreateInstanceAndUnwrap(
                        type.Assembly.FullName,
                        type.FullName);

                    var entryAssembly = proxy.LoadAssembly(EntryDll);

                    var result = entryAssembly.DeepCopy();

                    AppDomain.Unload(domain);

                    return result;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }).ConfigureAwait(false);
        }
    }

    public class ManagedAnalyserIsolation : MarshalByRefObject
    {
        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded;

        private string dllPath;

        public ManagedAnalyserIsolation() =>
            assembliesLoaded = new Dictionary<string, AssemblyInformation>();

        private AssemblyInformation EntryAssembly { get; set; }

        public AssemblyInformation LoadAssembly(string entryDll)
        {
            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            dllPath = baseDirectory;

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnCurrentDomainReflectionOnlyAssemblyResolve;

            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(entryDll);

            EntryAssembly = GetManaged(assembly.GetName(), baseDirectory, fileInfo.Extension.Replace(".", ""));

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
                    info.Links.AddRange(AnalyseManagedNative(info.FilePath, baseDirectory));
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
               IsLocalAssembly = asmToCheck != null,
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

        private AssemblyInformation CreateNativeAssemblyInformation(string fileName, string baseDirectory)
        {

            var filePath = GetAssemblyPath(fileName, baseDirectory);
            var isNativeSystem = false;

            if (string.IsNullOrEmpty(filePath))
            {
                var system32Path = Environment.SystemDirectory;

                var testedPath = Path.Combine(system32Path, fileName);
                if (File.Exists(testedPath))
                {
                    isNativeSystem = true;
                    filePath = testedPath;
                }
            }

            var info = new AssemblyInformation(fileName, null, filePath)
            {
                IsNative = true,
                IsLocalAssembly = !isNativeSystem,
            };

            info.EnhanceProperties();

            return info;
        }

        private string GetAssemblyPath(string fileName, string baseDirectory)
        {
            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }

        private IEnumerable<AssemblyLink> AnalyseManagedNative(string file, string baseDirectory)
        {
            var peHeader = new PeNet.PeFile(file);

            if (peHeader?.ImportedFunctions == null)
                yield break;

            var referencedDll = peHeader.ImportedFunctions.Select(x => x.DLL).Distinct();
            foreach (var item in referencedDll.Select(x => GetNative(x, baseDirectory)))
                yield return new AssemblyLink(item, item.LoadedVersion);
        }

        public AssemblyInformation GetNative(string dllFile, string baseDirectory)
        {
            if (assembliesLoaded.TryGetValue(dllFile, out AssemblyInformation assemblyFound))
                return assemblyFound;

            var info = CreateNativeAssemblyInformation(dllFile, baseDirectory);

            if (info.IsLocalAssembly && info.IsResolved)
            {
                var peHeader = new PeNet.PeFile(info.FilePath);
                var referencedDll = peHeader.ImportedFunctions.Select(x => x.DLL).Distinct();

                info.Links.AddRange(referencedDll.Select(x => 
                                                        {
                                                            var native = GetNative(x, baseDirectory);
                                                            return new AssemblyLink(native, native.LoadedVersion);
                                                        }));
            }

            assembliesLoaded.Add(dllFile, info);

            return info;
        }

    }
}
