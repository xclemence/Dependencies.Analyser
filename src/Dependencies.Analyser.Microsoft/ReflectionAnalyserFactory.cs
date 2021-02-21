using Dependencies.Analyser.Base;

namespace Dependencies.Analyser.Microsoft
{
    public class ReflectionAnalyserFactory : IAssemblyAnalyserFactory
    {
        private readonly IAnalyserServiceFactory serviceFactory;

        public ReflectionAnalyserFactory(IAnalyserServiceFactory serviceFactory) => this.serviceFactory = serviceFactory;

        public string Name => "Microsoft";
        public string Code => "Microsoft";

        public string Version => typeof(ReflectionAnalyserFactory).Assembly.GetName().Version?.ToString() ?? string.Empty;

        public IAssemblyAnalyser GetAnalyser() => serviceFactory.Create<ReflectionAnalyser>();
    }
}
