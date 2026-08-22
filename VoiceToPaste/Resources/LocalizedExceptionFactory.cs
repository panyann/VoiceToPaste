using System.Globalization;

namespace VoiceToPaste.Resources
{
    internal static class LocalizedExceptionFactory
    {
        private const string ResourceKeyDataKey = "VoiceToPaste.ResourceKey";
        private const string ResourceArgumentsDataKey = "VoiceToPaste.ResourceArguments";
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");

        public static InvalidOperationException InvalidOperation(string resourceKey, params object?[] arguments)
        {
            return Mark(new InvalidOperationException(GetEnglishMessage(resourceKey, arguments)), resourceKey, arguments);
        }

        public static InvalidOperationException InvalidOperation(
            string resourceKey,
            Exception innerException,
            params object?[] arguments)
        {
            return Mark(
                new InvalidOperationException(GetEnglishMessage(resourceKey, arguments), innerException),
                resourceKey,
                arguments);
        }

        public static InvalidDataException InvalidData(string resourceKey, params object?[] arguments)
        {
            return Mark(new InvalidDataException(GetEnglishMessage(resourceKey, arguments)), resourceKey, arguments);
        }

        public static ArgumentException Argument(string resourceKey, string parameterName, params object?[] arguments)
        {
            return Mark(
                new ArgumentException(GetEnglishMessage(resourceKey, arguments), parameterName),
                resourceKey,
                arguments);
        }

        public static ArgumentOutOfRangeException ArgumentOutOfRange(
            string resourceKey,
            string parameterName,
            params object?[] arguments)
        {
            return Mark(
                new ArgumentOutOfRangeException(parameterName, GetEnglishMessage(resourceKey, arguments)),
                resourceKey,
                arguments);
        }

        public static FileNotFoundException FileNotFound(string resourceKey, params object?[] arguments)
        {
            return Mark(new FileNotFoundException(GetEnglishMessage(resourceKey, arguments)), resourceKey, arguments);
        }

        public static string GetUserMessage(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return GetUserMessage(exception, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);
        }

        internal static string GetUserMessage(Exception exception, CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull(culture);
            return GetUserMessage(exception, culture, culture);
        }

        private static string GetUserMessage(
            Exception exception,
            CultureInfo resourceCulture,
            CultureInfo formatCulture)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (exception.Data[ResourceKeyDataKey] is not string resourceKey ||
                exception.Data[ResourceArgumentsDataKey] is not object?[] arguments)
            {
                return exception.Message;
            }

            return UiStrings.Format(resourceKey, resourceCulture, formatCulture, arguments);
        }

        private static string GetEnglishMessage(string resourceKey, object?[] arguments)
        {
            return UiStrings.Format(resourceKey, EnglishCulture, arguments);
        }

        private static T Mark<T>(T exception, string resourceKey, object?[] arguments)
            where T : Exception
        {
            exception.Data[ResourceKeyDataKey] = resourceKey;
            exception.Data[ResourceArgumentsDataKey] = arguments;
            return exception;
        }
    }
}
