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
using PeNet;

namespace Dependencies.Analyser
{
    public class MonoAnalyser : IManagedAnalyser
    {

        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded = new Dictionary<string, AssemblyInformation>();

        public MonoAnalyser(string entryDll)
        {
            EntryDll = entryDll;
        }

        public string EntryDll { get; }

        public async Task<AssemblyInformation> AnalyseAsync()
        {
            return await Task.Run(() => LoadManagedAssembly(EntryDll) ?? LoadNativeAssembly(EntryDll)).ConfigureAwait(false);
        }

        public AssemblyInformation LoadManagedAssembly(string entryDll)
        {
            try
            {
                var fileInfo = new FileInfo(entryDll);
                var baseDirectory = Path.GetDirectoryName(entryDll);

                var assembly = AssemblyDefinition.ReadAssembly(entryDll);

                return GetManaged(assembly.Name, baseDirectory, fileInfo.Extension.Replace(".", ""));
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public AssemblyInformation LoadNativeAssembly(string entryDll)
        {
            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            return GetNative(fileInfo.Name, baseDirectory); 
        }


        public AssemblyInformation GetManaged(AssemblyNameReference assemblyDefinition, string baseDirectory, string extension = "dll")
        {
            if (assembliesLoaded.TryGetValue(assemblyDefinition.Name, out AssemblyInformation assemblyFound))
                return assemblyFound;

            var (info, assembly) = CreateManagedAssemblyInformation(assemblyDefinition, baseDirectory, extension);

            assembliesLoaded.Add(assemblyDefinition.Name, info);

            if (assembly != null && info.IsLocalAssembly)
            {
                info.Links.AddRange(assembly.MainModule.AssemblyReferences.Select(x => new AssemblyLink(GetManaged(x, baseDirectory), x.Version.ToString())));

                if (!info.IsILOnly)
                    info.Links.AddRange(AnalyseManagedNative(info.FilePath, baseDirectory));
            }

            return info;
        }

        private (AssemblyInformation info, AssemblyDefinition assembly) CreateManagedAssemblyInformation(AssemblyNameReference assemblyName, string baseDirectory, string extension = "dll")
        {
            var assemblyPath = GetAssemblyPath($"{assemblyName.Name}.{extension}", baseDirectory);

            AssemblyDefinition assembly = null;
            try
            {
                var resolver = new DefaultAssemblyResolver();
                assembly = assemblyPath != null ? AssemblyDefinition.ReadAssembly(assemblyPath) : resolver.Resolve(assemblyName); ;
            }
            catch (Exception ex)
            {
                // do norting
            }

            var info = new AssemblyInformation(assemblyName.Name, assembly?.Name.Version.ToString() ?? assemblyName.Version.ToString(), assemblyPath)
            {
                IsLocalAssembly = assemblyPath != null,
                AssemblyName = assembly?.FullName
            };

            try
            {
                info.EnhancePropertiesWithFile();

                if (assembly != null)
                    info.EnhanceProperties(assembly);
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

            info.EnhancePropertiesWithFile();

            return info;
        }

        private string GetAssemblyPath(string fileName, string baseDirectory)
        {
            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }

        private IEnumerable<AssemblyLink> AnalyseManagedNative(string file, string baseDirectory)
        {
            var referencedDlls = PeFile.GetImportedPeFile(file).Select(x => x.DLL).Distinct();

            foreach (var item in referencedDlls.Select(x => GetNative(x, baseDirectory)))
                yield return new AssemblyLink(item, item.LoadedVersion);
        }

        public AssemblyInformation GetNative(string dllFile, string baseDirectory)
        {
            if (assembliesLoaded.TryGetValue(dllFile, out AssemblyInformation assemblyFound))
                return assemblyFound;

            var info = CreateNativeAssemblyInformation(dllFile, baseDirectory);

            if (info.IsLocalAssembly && info.IsResolved)
            {
                var referencedDlls = PeFile.GetImportedPeFile(info.FilePath).Select(x => x.DLL).Distinct();

                info.TargetProcessor = PeFile.Is64BitPeFile(info.FilePath) ? TargetProcessor.x64 : TargetProcessor.x86;

                info.Links.AddRange(referencedDlls.Select(x => 
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
