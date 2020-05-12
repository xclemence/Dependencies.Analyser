using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dependencies.Analyser.Base.Models;

namespace Dependencies.Analyser.Base.Extensions
{
    public static class AssemblyInformationExtensions
    {
        public static void EnhancePropertiesWithFile(this AssemblyInformation info)
        {
            if (info is null)
                throw new System.ArgumentNullException(nameof(info));

            if (!info.IsLocalAssembly || !info.IsResolved)
                return;

            if (!File.Exists(info.FilePath))
                return;

            var fileInfo = new FileInfo(info.FilePath);
            info.CreationDate = fileInfo.CreationTime;

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(info.FilePath);
            info.Creator = fileVersionInfo.CompanyName;

            if (string.IsNullOrEmpty(info.LoadedVersion))
                info.LoadedVersion = fileVersionInfo.ProductVersion;

            if (info.IsDebug.HasValue)
                info.IsDebug = fileVersionInfo.IsDebug;
        }

        public static IEnumerable<AssemblyLink> GetAllLinks(this AssemblyInformation assembly)
        {
            if (assembly is null)
                throw new System.ArgumentNullException(nameof(assembly));

            foreach (var item in assembly.Links)
            {
                yield return item;

                foreach (var subItem in item.Assembly.GetAllLinks())
                    yield return subItem;
            }
        }

        public static AssemblyInformation RemoveChildenLoop(this AssemblyInformation assembly)
        {
            if (assembly is null)
                throw new System.ArgumentNullException(nameof(assembly));

            var path = new List<AssemblyInformation> { assembly };
            var newDependencies = assembly.Links.Where(x => x.Assembly.RemoveChildenLoop(path)).ToList();

            assembly.Links.Clear();
            assembly.Links.AddRange(newDependencies);
            return assembly;
        }

        private static bool RemoveChildenLoop(this AssemblyInformation assembly, IEnumerable<AssemblyInformation> path)
        {
            var currentPath = path.ToList();

            if (currentPath.Contains(assembly))
                return false;

            currentPath.Add(assembly);

            var newDependencies = assembly.Links.Where(x => RemoveChildenLoop(x.Assembly, currentPath)).ToList();

            assembly.Links.Clear();
            assembly.Links.AddRange(newDependencies);

            return true;
        }
    }
}
