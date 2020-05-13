using Dependencies.Analyser.Base;

namespace Dependencies.Analyser.Microsoft
{
    public class ReflectionAnalyserFactory : IAssemblyAnalyserFactory
    {
        private readonly IAnalyserServiceFactory<ReflectionAnalyser> serviceFactory;

        public ReflectionAnalyserFactory(IAnalyserServiceFactory<ReflectionAnalyser> serviceFactory) => this.serviceFactory = serviceFactory;

        public string Name => "Microsoft";
        public string Code => "Microsoft";

        public IAssemblyAnalyser GetAnalyser() => serviceFactory.Create();
    }
}
