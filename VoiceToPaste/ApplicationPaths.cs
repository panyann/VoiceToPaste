namespace VoiceToPaste
{
    /// <summary>
    /// Wyznacza trwały katalog aplikacji na podstawie rzeczywistego pliku wykonywalnego.
    /// Nie używamy AppContext.BaseDirectory, ponieważ w publikacji single-file może on
    /// wskazywać tymczasowy katalog ekstrakcji środowiska .NET.
    /// </summary>
    internal static class ApplicationPaths
    {
        public static string Directory { get; } = GetApplicationDirectory();

        private static string GetApplicationDirectory()
        {
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("The application executable path could not be determined.");

            return Path.GetDirectoryName(executablePath)
                ?? throw new InvalidOperationException("The application executable directory could not be determined.");
        }
    }
}
