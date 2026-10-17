
namespace UploadBox.Models
{
    public enum UploadPolicy
    {
        Strict,
        SkipInvalidFiles
    }

    public sealed class UploadOptions
    {
        private const int DEFAULT_BUFFER_SIZE = 1024 * 1024;

        public const long DEFAULT_MAX_FILE_SIZE_BYTES = 1 * 1024 * 1024;

        public int MaxFileCount { get; }

        public long MaxFileSizeBytes { get; }

        public bool AllowMultipleFiles => MaxFileCount > 1;

        public string FileExtension { get; }

        public UploadPolicy Policy { get; }

        public int BufferSize { get; }

        public UploadOptions(int maxFileCount = 1, long maxFileSizeBytes = DEFAULT_MAX_FILE_SIZE_BYTES, UploadPolicy policy = UploadPolicy.Strict, string? fileExtension = null, int bufferSize = DEFAULT_BUFFER_SIZE)
        {
            if (maxFileCount < 1)
                throw new ArgumentOutOfRangeException(nameof(maxFileCount));

            if (maxFileSizeBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes));

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            if (!Enum.IsDefined(policy))
                throw new ArgumentOutOfRangeException(nameof(policy));

            MaxFileCount = maxFileCount;
            MaxFileSizeBytes = maxFileSizeBytes;
            FileExtension = NormalizeExtension(fileExtension);
            Policy = policy;
            BufferSize = bufferSize;
        }

        private static string NormalizeExtension(string? extension)
        {
            string normalizedExtension = extension?.Trim() ?? string.Empty;

            if (normalizedExtension.Length == 0)
                return string.Empty;

            return normalizedExtension.StartsWith('.') ? normalizedExtension : $".{normalizedExtension}";
        }
    }
}
