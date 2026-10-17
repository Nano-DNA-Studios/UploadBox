
namespace UploadBox.Models
{
    /// <summary>
    /// Specifies how a selection containing rejected files is handled.
    /// </summary>
    public enum UploadPolicy
    {
        /// <summary>
        /// Blocks the upload when any selected file is rejected.
        /// </summary>
        Strict,

        /// <summary>
        /// Allows accepted files to be uploaded while preventing the rejected files.
        /// </summary>
        SkipInvalidFiles
    }

    /// <summary>
    /// Defines file-count, file-size, extension, policy, and buffering options for uploads.
    /// </summary>
    public sealed class UploadOptions
    {
        /// <summary>
        /// The Default Buffer size of 1MB
        /// </summary>
        private const int DEFAULT_BUFFER_SIZE = 1024 * 1024;

        /// <summary>
        /// Default maximum file size, in bytes.
        /// </summary>
        public const long DEFAULT_MAX_FILE_SIZE_BYTES = 1 * 1024 * 1024;

        /// <summary>
        /// Gets the maximum number of files allowed in one selection.
        /// </summary>
        public int MaxFileCount { get; }

        /// <summary>
        /// Gets the maximum allowed size of each file, in bytes.
        /// </summary>
        public long MaxFileSizeBytes { get; }

        /// <summary>
        /// Gets whether the options allow selecting more than one file.
        /// </summary>
        public bool AllowMultipleFiles => MaxFileCount > 1;

        /// <summary>
        /// Gets the required file extension, or an empty string when no extension is required.
        /// </summary>
        public string FileExtension { get; }

        /// <summary>
        /// Gets the policy used when a selection contains rejected files.
        /// </summary>
        public UploadPolicy Policy { get; }

        /// <summary>
        /// Gets the buffer size, in bytes, used while staging a file.
        /// </summary>
        public int BufferSize { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="UploadOptions"/>.
        /// </summary>
        /// <param name="maxFileCount">Maximum number of files allowed in one selection.</param>
        /// <param name="maxFileSizeBytes">Maximum allowed size for each file, in bytes.</param>
        /// <param name="policy">Policy used when the selection contains rejected files.</param>
        /// <param name="fileExtension">Optional required extension, with or without a leading period.</param>
        /// <param name="bufferSize">Buffer size, in bytes, used while staging a file.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a count or size is invalid or the policy is undefined.</exception>
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

        /// <summary>
        /// Trims an extension and adds a leading period when one is absent.
        /// </summary>
        /// <param name="extension">Extension to normalize, or <see langword="null"/>.</param>
        /// <returns>An empty string for a blank extension; otherwise, the normalized extension.</returns>
        private static string NormalizeExtension(string? extension)
        {
            string normalizedExtension = extension?.Trim() ?? string.Empty;

            if (normalizedExtension.Length == 0)
                return string.Empty;

            return normalizedExtension.StartsWith('.') ? normalizedExtension : $".{normalizedExtension}";
        }
    }
}
