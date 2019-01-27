namespace Dependencies.Analyser.Base
{
    public interface IServiceFactory<T>
    {
        T Create();
    }
}
