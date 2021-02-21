namespace Dependencies.Analyser.Base
{
    public interface IAnalyserServiceFactory
    {
        T Create<T>() where T : class;
    }
}
