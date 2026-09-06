namespace VoiceToPaste.Services
{
    public static class ApplicationVersionService
    {
        /// <summary>
        /// Returns the application version without build metadata, prefixed with "v".
        /// </summary>
        public static string GetVersion()
        {
            var productVersion = Application.ProductVersion;
            var metadataSeparatorIndex = productVersion.IndexOf('+');
            if (metadataSeparatorIndex >= 0)
            {
                productVersion = productVersion[..metadataSeparatorIndex];
            }

            return $"v{productVersion}";
        }
    }
}
