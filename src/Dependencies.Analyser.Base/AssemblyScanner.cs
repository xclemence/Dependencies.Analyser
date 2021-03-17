using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base
{
    public record AssemblyName(string FullName, string Name, string Version);

    public interface IScannerPlugin
    {
        AssemblyInformation GetAssembly(string path);
        AssemblyInformation GetAssembly(AssemblyName assemblyName);

        AssemblyName GetAssemblyName(string path);

        IEnumerable<AssemblyName> GetReferences(AssemblyInformation assembly);
    }


    public class AssemblyScanner : BaseAnalyser
    {
        private readonly IScannerPlugin managedScanner;
        private readonly IScannerPlugin nativeScanner;

        public AssemblyScanner(IAnalyserSettingProvider settings, IScannerPlugin managedScanner, IScannerPlugin nativeScanner):
            base(settings)
        {
            this.managedScanner = managedScanner;
            this.nativeScanner = nativeScanner;
            //NativeAnalyser = new NativeAnalyser(settings, AssembliesLoaded, LinksLoaded);
        }

        protected override (AssemblyInformation assembly, IDictionary<string, AssemblyLink> links) Analyse(string dllPath)
        {
            var baseDirectory = Path.GetDirectoryName(dllPath);

            var assemblyName = managedScanner.GetAssemblyName(dllPath);
            var assembly = GetAssembly(dllPath, assemblyName);
            AddLoadedAssemblies(assembly, assembly.FullName, baseDirectory);

            return (assembly, LinksLoaded);
        }

        private AssemblyInformation GetAssembly(string path, AssemblyName assemblyName)
        {
            if (AssembliesLoaded.TryGetValue(assemblyName.FullName, out var assembly))
                return assembly;

            assembly = path == null ? managedScanner.GetAssembly(assemblyName) : managedScanner.GetAssembly(path);

            AssembliesLoaded.Add(assembly.FullName, assembly);

            return assembly;
        }

        private void AddLoadedAssemblies(AssemblyInformation assembly, string parentAssemblyName, string baseDirectory)
        {
            if (assembly.IsLocalAssembly || Settings.GetSetting<bool>(SettingKeys.ScanGlobalManaged))
            {
                var dependencies = managedScanner.GetReferences(assembly);

                assembly.Links.AddRange(dependencies.Select(x => GetAssemblyLink(x, parentAssemblyName, baseDirectory)));

                //if (!assembly.IsILOnly && Settings.GetSetting<bool>(SettingKeys.ScanCliReferences) && assembly.FilePath != null)
                //    assembly.Links.AddRange(NativeAnalyser.GetNativeLinks(assembly, parentAssemblyName, baseDirectory));

                //if (Settings.GetSetting<bool>(SettingKeys.ScanDllImport))
                //    AppendDllImportDll(assembly, monoAssembly, parentAssemblyName, baseDirectory);
            }
        }


        public AssemblyLink GetAssemblyLink(AssemblyName assemblName, string parentName, string baseDirectory)
        {
            if (LinksLoaded.TryGetValue(assemblName.FullName, out var assemblyLink))
            {
                assemblyLink.Assembly?.ParentLinkName.Add(parentName);
                return assemblyLink;
            }

            var assemblyPath = FilePathProvider.GetAssemblyPath($"{assemblName.Name}.dll", baseDirectory);
            var assembly = GetAssembly(assemblyPath, assemblName);

            var newAssemblyLink = new AssemblyLink(assembly, assemblName.Version, assemblName.FullName);

            LinksLoaded.Add(assemblName.FullName, newAssemblyLink);

            AddLoadedAssemblies(assembly, assemblName.FullName, baseDirectory);
            newAssemblyLink.Assembly.ParentLinkName.Add(parentName);

            return newAssemblyLink;
        }


        public static bool IsManagedAssembly(string fileName)
        {
            using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            using var binaryReader = new BinaryReader(fileStream);
            if (fileStream.Length < 64)
                return false;

            fileStream.Position = 0x3C;
            uint peHeaderPointer = binaryReader.ReadUInt32();
            if (peHeaderPointer == 0)
                peHeaderPointer = 0x80;

            if (peHeaderPointer > fileStream.Length - 256)
                return false;

            fileStream.Position = peHeaderPointer;
            uint peHeaderSignature = binaryReader.ReadUInt32();
            if (peHeaderSignature != 0x00004550)
                return false;

            fileStream.Position += 20;

            const ushort PE32 = 0x10b;
            const ushort PE32Plus = 0x20b;

            var peFormat = binaryReader.ReadUInt16();
            if (peFormat != PE32 && peFormat != PE32Plus)
                return false;

            ushort dataDictionaryStart = (ushort)(peHeaderPointer + (peFormat == PE32 ? 232 : 248));
            fileStream.Position = dataDictionaryStart;

            uint cliHeaderRva = binaryReader.ReadUInt32();
            if (cliHeaderRva == 0)
                return false;

            return true;
        }
    }
}
