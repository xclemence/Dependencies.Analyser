namespace Dependencies.Analyser.Base
{
    public interface IAssemblyAnalyserFactory
    {
        string Name { get; }

        string Code { get; }
        
        string Version { get; }

        IAssemblyAnalyser GetAnalyser();
    }
}
