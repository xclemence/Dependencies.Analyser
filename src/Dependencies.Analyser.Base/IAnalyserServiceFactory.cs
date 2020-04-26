namespace Dependencies.Analyser.Base
{
    public interface IAnalyserServiceFactory<T>
    {
        T Create();
    }
}
