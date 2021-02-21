using Dependencies.Analyser.Base;

namespace Dependencies.Analyser.Mono
{
    public class MonoAnalyserFactory : IAssemblyAnalyserFactory
    {
        private readonly IAnalyserServiceFactory serviceFactory;

        public MonoAnalyserFactory(IAnalyserServiceFactory serviceFactory) =>
            this.serviceFactory = serviceFactory;

        public string Name => "Mono";
        public string Code => "Mono";

        public string Version => typeof(MonoAnalyserFactory).Assembly.GetName().Version?.ToString() ?? string.Empty;

        public IAssemblyAnalyser GetAnalyser() => serviceFactory.Create<MonoAnalyser>();
    }
}
