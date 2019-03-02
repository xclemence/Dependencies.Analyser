namespace Dependencies.Analyser.Base
{
    public class SettingKeys
    {
        public static string SelectedAnalyserCode => "SelectedAnalyserCode";
        public static string ScanGlobalNative => "ScanGlobalNative";
        public static string ScanGlobalManaged => "ScanGlobalManaged";
        public static string ScanDllImport => "ScanDllImport";
        public static string ScanCliReferences => "ScanCliReferences";
    }

    public interface ISettingProvider
    {
        void SaveSetting<T>(string code, T value);

        T GetSettring<T>(string code);

        dynamic this[string code] { get; set; }
    }
}
