using Dependencies.Analyser.Base;

namespace Dependencies.Analyser.Mono
{
    public class MonoAnalyserFactory : IAssemblyAnalyserFactory
    {
        private readonly IServiceFactory<MonoAnalyser> serviceFactory;

        public MonoAnalyserFactory(IServiceFactory<MonoAnalyser> serviceFactory)
        {
            this.serviceFactory = serviceFactory;
        }

        public string Name => "Mono";
        public string Code => "Mono";

        public IAssemblyAnalyser GetAnalyser() => serviceFactory.Create();
    }
}
