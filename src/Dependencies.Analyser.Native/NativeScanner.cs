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
    public class NativeScanner : IScannerPlugin
    {
        private readonly IDictionary<string, AssemblyInformation> assembliesLoaded;
        private readonly IDictionary<string, AssemblyLink> links;
        private readonly IDictionary<string, string> windowsApiMap;
        private readonly bool scanGlobalAssemblies;

        public NativeScanner()
        {
            //this.assembliesLoaded = assembliesLoaded;
            //this.links = links;
            //scanGlobalAssemblies = setting?.GetSetting<bool>(SettingKeys.ScanGlobalNative) ?? false;

            var apiMapProvider = new ApiSetMapProviderInterop();
            var baseMap = apiMapProvider.GetApiSetMap();

            windowsApiMap = baseMap.Select(x => (key: x.Key, target: x.Value.FirstOrDefault(a => string.IsNullOrEmpty(a.alias)).name))
                                    .ToDictionary(x => $"{x.key.ToUpperInvariant()}.DLL", x => x.target);
        }


        public AssemblyInformation GetAssembly(string path)
        {
            var fileInfo = new FileInfo(path);
            var baseDirectory = Path.GetDirectoryName(path);

            var (file, filePath, isSystem) = GetFilePath(fileInfo.Name, baseDirectory);

            return CreateNativeAssemblyInformation(file, filePath, isSystem);
        }

        public AssemblyInformation GetAssembly(AssemblyName assemblyName) => throw new NotImplementedException();

        public AssemblyName GetAssemblyName(string path) => throw new NotImplementedException();

        public IEnumerable<AssemblyName> GetReferences(AssemblyInformation assembly) => throw new NotImplementedException();

        private string GetSystemFile(string fileName)
        {
            if (windowsApiMap.TryGetValue(fileName.ToUpperInvariant(), out var file))
                return file;

            return fileName;
        }

        private (string file, string? filePath, bool isSystem) GetFilePath(string fileName, string? baseDirectory)
        {
            var file = fileName;
            var filePath = GetAssemblyPath(fileName, baseDirectory);
            var isSystem = false;

            if (string.IsNullOrEmpty(filePath))
            {
                file = GetSystemFile(fileName);

                var system32Path = Environment.SystemDirectory;

                var testedPath = Path.Combine(system32Path, file);

                if (File.Exists(testedPath))
                {
                    isSystem = true;
                    filePath = testedPath;
                }
            }

            return (file, filePath, isSystem);
        }

        private AssemblyInformation CreateNativeAssemblyInformation(string fileName, string? filePath, bool isSystem)
        {
            var info = new AssemblyInformation(fileName, null, filePath)
            {
                IsNative = true,
                IsLocalAssembly = !isSystem,
                IsResolved = isSystem || filePath != null
            };

            info.EnhancePropertiesWithFile();

            if (info.IsResolved && info.FilePath != null)
            {
                var peFile = new PeFile(info.FilePath);
                info.TargetProcessor = peFile.Is64Bit ? TargetProcessor.x64 : TargetProcessor.x86;
            }

            return info;
        }

        private static string? GetAssemblyPath(string fileName, string? baseDirectory)
        {
            if (baseDirectory == null)
                return null;

            if (!fileName.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) && !fileName.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                fileName = $"{ fileName }.dll";
            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }
    }
}
