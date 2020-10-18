using System.Diagnostics;
using System.IO;
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
    }
}
