using VoiceToPaste.Resources;

namespace VoiceToPaste.Models
{
    /// <summary>Stały katalog wspieranych, wielojęzycznych modeli Whisper.</summary>
    public static class WhisperModelCatalog
    {
        private const string RepositoryUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

        private static readonly WhisperModel[] Models =
        [
            Create("tiny", "Tiny", "WhisperModelTinyDescription", "ggml-tiny.bin", 77_691_713,
                "BE07E048E1E599AD46341C8D2A135645097A538221678B7ACDD1B1919C6E1B21"),
            Create("base", "Base", "WhisperModelBaseDescription", "ggml-base.bin", 147_951_465,
                "60ED5BC3DD14EEA856493D334349B405782DDCAF0028D4B5DF4088345FBA2EFE"),
            Create("small", "Small", "WhisperModelSmallDescription", "ggml-small.bin", 487_601_967,
                "1BE3A9B2063867B937E64E2EC7483364A79917E157FA98C5D94B5C1FFFEA987B"),
            Create("medium", "Medium", "WhisperModelMediumDescription", "ggml-medium.bin", 1_533_763_059,
                "6C14D5ADEE5F86394037B4E4E8B59F1673B6CEE10E3CF0B11BBDBEE79C156208"),
            Create("large-v3-turbo", "Large v3 Turbo", "WhisperModelLargeV3TurboDescription", "ggml-large-v3-turbo.bin", 1_624_555_275,
                "1FC70F774D38EB169993AC391EEA357EF47C88757EF72EE5943879B7E8E2BC69"),
            Create("large-v3", "Large v3", "WhisperModelLargeV3Description", "ggml-large-v3.bin", 3_095_033_483,
                "64D182B440B98D5203C4F9BD541544D84C605196C4F7B845DFA11FB23594D1E2"),
        ];

        public const string DefaultModelId = "large-v3";

        public static IReadOnlyList<WhisperModel> GetAll() => Models;

        public static WhisperModel GetById(string id)
        {
            var model = TryGetById(id);
            return model ?? throw LocalizedExceptionFactory.Argument("UnsupportedWhisperModel", nameof(id), id);
        }

        public static WhisperModel? TryGetById(string? id) =>
            Models.FirstOrDefault(model => string.Equals(model.Id, id, StringComparison.Ordinal));

        private static WhisperModel Create(
            string id,
            string displayName,
            string descriptionKey,
            string fileName,
            long sizeBytes,
            string sha256) =>
            new(
                id,
                displayName,
                descriptionKey,
                fileName,
                sizeBytes,
                new Uri(RepositoryUrl + fileName + "?download=true"),
                sha256);
    }
}
