using System.Linq;
using Dependencies.Analyser.Base.Models;
using Dependencies.Analyser.Base;

namespace Dependencies.Analyser.Microsoft.Extensions
{
    public static class DeepCopyExtensions
    {
        public static AssemblyInformation DeepCopy(this AssemblyInformation item)
        {
            var cache = new ObjectCacheTransformer();
            return item.DeepCopy(cache);
        }

        public static AssemblyInformation DeepCopy(this AssemblyInformation baseItem, ObjectCacheTransformer transformer)
        {
            var newItem = transformer.Transform(baseItem, (x => x.DeepCopyBase()));

            newItem.Links = baseItem.Links?.Select(x => x.DeepCopy(transformer)).ToList();

            return newItem;
        }

        public static AssemblyInformation DeepCopyBase(this AssemblyInformation baseItem)
        {
            var newItem = new AssemblyInformation(baseItem.Name, baseItem.LoadedVersion, baseItem.FilePath)
            {
                CreationDate = baseItem.CreationDate,
                Creator = baseItem.Creator,
                IsDebug = baseItem.IsDebug,
                AssemblyName = baseItem.AssemblyName,
                IsILOnly = baseItem.IsILOnly,
                IsLocalAssembly = baseItem.IsLocalAssembly,
                IsNative = baseItem.IsNative,
                TargetProcessor = baseItem.TargetProcessor
            };

            return newItem;
        }

        internal static AssemblyLink DeepCopy(this AssemblyLink baseItem, ObjectCacheTransformer transformer)
        {
            return new AssemblyLink(transformer.Transform(baseItem, x => x.Assembly.DeepCopy(transformer)), baseItem.LinkVersion);
        }
    }
}
