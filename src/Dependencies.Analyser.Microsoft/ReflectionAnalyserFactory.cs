using Dependencies.Analyser.Base;

namespace Dependencies.Analyser.Microsoft
{
    public class ReflectionAnalyserFactory : IAssemblyAnalyserFactory
    {
        private readonly IServiceFactory<ReflectionAnalyser> serviceFactory;

        public ReflectionAnalyserFactory(IServiceFactory<ReflectionAnalyser> serviceFactory)
        {
            this.serviceFactory = serviceFactory;
        }

        public string Name => "Microsoft";
        public string Code => "Microsoft";

        public IAssemblyAnalyser GetAnalyser() => serviceFactory.Create();
    }
}
