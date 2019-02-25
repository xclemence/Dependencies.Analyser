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

        public NativeAnalyser()
        {
            assembliesLoaded = new Dictionary<string, AssemblyInformation>();

            var apiMapProvider = new ApiSetMapProviderInterop();
            var baseMap = apiMapProvider.GetApiSetMap();

            windowsApiMap = baseMap.Select(x => (key: x.Key,  target: x.Value.FirstOrDefault(a => string.IsNullOrEmpty(a.alias)).name))
                                    .ToDictionary(x => $"{x.key}.dll", x => x.target);
        }

        public AssemblyInformation LoadNativeAssembly(string entryDll)
        {
            var fileInfo = new FileInfo(entryDll);
            var baseDirectory = Path.GetDirectoryName(entryDll);

            return GetNative(fileInfo.Name, baseDirectory); 
        }


        private string GetSystemFile(string fileName)
        {
            if (windowsApiMap.TryGetValue(fileName, out string file))
                return file;

            return fileName;
        }

        public (string file, string filePath, bool isSystem) GetFilePath(string fileName, string baseDirectory)
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
            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }

        public IEnumerable<AssemblyLink> GetNativeLinks(string file, string baseDirectory)
        {
            var referencedDlls = PeFile.GetImportedPeFile(file).Select(x => x.DLL).Distinct();

            foreach (var item in referencedDlls.Select(x => GetNative(x, baseDirectory)))
                yield return new AssemblyLink(item, item.LoadedVersion);
        }

        public AssemblyInformation GetNative(string dllFile, string baseDirectory)
        {
            var (file, filePath, isSystem) = GetFilePath(dllFile, baseDirectory);

            if (assembliesLoaded.TryGetValue(file, out AssemblyInformation assemblyFound))
                return assemblyFound;

            var info = CreateNativeAssemblyInformation(file, filePath, isSystem);

            if (info.IsResolved)
            {
                var referencedDlls = PeFile.GetImportedPeFile(info.FilePath).Select(x => x.DLL).Distinct();

                info.TargetProcessor = PeFile.Is64BitPeFile(info.FilePath) ? TargetProcessor.x64 : TargetProcessor.x86;

                if(info.IsLocalAssembly)
                {
                    info.Links.AddRange(referencedDlls.Select(x =>
                    {
                        var native = GetNative(x, baseDirectory);
                        return new AssemblyLink(native, native.LoadedVersion);
                    }));
                }
            }

            assembliesLoaded.Add(file, info);

            return info;
        }
    }
}
