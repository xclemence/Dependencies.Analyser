using System.IO;

namespace Dependencies.Analyser.Base
{
    public static class FilePathProvider
    {
        public static string? GetAssemblyPath(string fileName, string? baseDirectory)
        {
            if (baseDirectory == null)
                return null;

            var result = Directory.GetFiles(baseDirectory, fileName, SearchOption.AllDirectories);

            return result.Length != 0 ? result[0] : null;
        }
    }
}
