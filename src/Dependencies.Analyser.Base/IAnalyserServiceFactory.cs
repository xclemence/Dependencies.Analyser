namespace Dependencies.Analyser.Base
{
    public interface IAnalyserServiceFactory<out T> where T : class
    {
        T Create();
    }
}
