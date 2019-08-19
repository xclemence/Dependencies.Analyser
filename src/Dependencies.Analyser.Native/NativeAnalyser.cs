using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependencies.Analyser.Base;
using Dependencies.Analyser.Base.Extensions;
using Dependencies.Analyser.Base.Models;
using Dependencies.ApiSetMapInterop;
using PeNet;

namespace Dependencies.Analyser.Native
{
    public class NativeAnalyser : INativeAnalyser
    {
        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded;
        private readonly IDictionary<string, string> windowsApiMap;
        private readonly bool scanGlobalAssemblies;


        public NativeAnalyser(ISettingProvider setting)
        {
            assembliesLoaded = new Dictionary<string, AssemblyInformation>();
            scanGlobalAssemblies = setting.GetSettring<bool>(SettingKeys.ScanGlobalNative);

            var apiMapProvider = new ApiSetMapProviderInterop();
            var baseMap = apiMapProvider.GetApiSetMap();

            windowsApiMap = baseMap.Select(x => (key: x.Key,  target: x.Value.FirstOrDefault(a => string.IsNullOrEmpty(a.alias)).name))
                                    .ToDictionary(x => $"{x.key.ToLower()}.dll", x => x.target);
        }

        public AssemblyInformation LoadNativeAssembly(string entryDll)
        {
            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            var (file, filePath, isSystem) = GetFilePath(fileInfo.Name, baseDirectory);
            return GetNative(file, filePath, isSystem, baseDirectory); 
        }

        private string GetSystemFile(string fileName)
        {
            if (windowsApiMap.TryGetValue(fileName, out string file))
                return file;

            return fileName;
        }

        private (string file, string filePath, bool isSystem) GetFilePath(string fileName, string baseDirectory)
        {
            var file = fileName;
            var filePath = GetAssemblyPath(fileName, baseDirectory);
            var isSystem = false;

            if (string.IsNullOrEmpty(filePath))
            {
                file = GetSystemFile(fileName);

                var system32Path = Environment.SystemDirectory;

                var  testedPath = Path.Combine(system32Path, file);

                if (File.Exists(testedPath))
                {
                    isSystem = true;
                    filePath = testedPath;
                }
            }

            return (file, filePath, isSystem);
        }

        private AssemblyInformation CreateNativeAssemblyInformation(string fileName, string filePath, bool isSystem)
        {
            var info = new AssemblyInformation(fileName, null, filePath)
            {
                IsNative = true,
                IsLocalAssembly = !isSystem,
            };

            info.EnhancePropertiesWithFile();

            return info;
        }

        private string GetAssemblyPath(string fileName, string baseDirectory)
        {
            if (!fileName.EndsWith(".dll"))
                fileName = $"{ fileName }.dll";
            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }

        public IEnumerable<AssemblyLink> GetNativeLinks(string file, string baseDirectory)
        {
            var referencedDlls = PeFile.GetImportedPeFile(file)
                                       .Select(x => GetFilePath(x.DLL, baseDirectory))
                                       .Where(x => !string.Equals(x.file, file, StringComparison.InvariantCultureIgnoreCase))
                                       .Distinct()
                                       .Select(x => GetNative(x.file, x.filePath, x.isSystem, baseDirectory));

            foreach (var item in referencedDlls)
                yield return new AssemblyLink(item, item.LoadedVersion);
        }

        public AssemblyLink GetNativeLink(string dllName, string baseDirectory)
        {
            var (file, filePath, isSystem) = GetFilePath(dllName, baseDirectory);
            var assembly = GetNative(file, filePath, isSystem, baseDirectory);

            return new AssemblyLink(assembly, assembly.LoadedVersion);
        }

        public AssemblyInformation GetNative(string fileName, string filePath, bool isSystem, string baseDirectory)
        {
            if (assembliesLoaded.TryGetValue(fileName.ToLower(), out var assemblyFound))
                return assemblyFound;

            var info = CreateNativeAssemblyInformation(fileName, filePath, isSystem);
            assembliesLoaded.Add(fileName.ToLower(), info);

            if (info.IsResolved)
            {
                var referencedDlls = PeFile.GetImportedPeFile(info.FilePath).Select(x => x.DLL).Distinct().ToList();

                info.TargetProcessor = PeFile.Is64BitPeFile(info.FilePath) ? TargetProcessor.x64 : TargetProcessor.x86;

                if (scanGlobalAssemblies || info.IsLocalAssembly)
                {
                    info.Links.AddRange(referencedDlls.Select(x => GetFilePath(x, baseDirectory)).Distinct().Select(x =>
                    {
                        var native = GetNative(x.file, x.filePath, x.isSystem, baseDirectory);
                        return new AssemblyLink(native, native.LoadedVersion);
                    }));
                }
            }

            return info;
        }
    }
}
