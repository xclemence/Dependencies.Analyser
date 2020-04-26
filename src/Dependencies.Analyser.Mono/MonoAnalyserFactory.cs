using Dependencies.Analyser.Base;

namespace Dependencies.Analyser.Mono
{
    public class MonoAnalyserFactory : IAssemblyAnalyserFactory
    {
        private readonly IAnalyserServiceFactory<MonoAnalyser> serviceFactory;

        public MonoAnalyserFactory(IAnalyserServiceFactory<MonoAnalyser> serviceFactory)
        {
            this.serviceFactory = serviceFactory;
        }

        public string Name => "Mono";
        public string Code => "Mono";

        public IAssemblyAnalyser GetAnalyser() => serviceFactory.Create();
    }
}
