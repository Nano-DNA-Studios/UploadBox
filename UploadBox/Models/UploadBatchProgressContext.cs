namespace UploadBox.Models
{
    /// <summary>
    /// Provides the Upload batches current progress
    /// </summary>
    public sealed record UploadBatchProgressContext
    {
        /// <summary>
        /// Gets the index of the current file in the batch.
        /// </summary>
        public int FileIndex { get; }

        /// <summary>
        /// Gets the total number of files in the batch.
        /// </summary>
        public int FileCount { get; }

        /// <summary>
        /// Gets the number of bytes belonging to earlier files in the batch.
        /// </summary>
        public long CompletedBytesBeforeCurrentFile { get; }

        /// <summary>
        /// Gets the combined size of all files in the batch, in bytes.
        /// </summary>
        public long TotalSizeBytes { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="UploadBatchProgressContext"/>.
        /// </summary>
        /// <param name="fileIndex">One-based index of the current file.</param>
        /// <param name="fileCount">Total number of files in the batch.</param>
        /// <param name="completedBytesBeforeCurrentFile">Bytes in files preceding the current file.</param>
        /// <param name="totalSizeBytes">Combined size of the batch in bytes.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the file count, file index, completed byte count, or total size is invalid.</exception>
        public UploadBatchProgressContext(int fileIndex, int fileCount, long completedBytesBeforeCurrentFile, long totalSizeBytes)
        {
            if (fileCount < 1)
                throw new ArgumentOutOfRangeException(nameof(fileCount), "File count must be positive.");
            if (fileIndex < 1 || fileIndex > fileCount)
                throw new ArgumentOutOfRangeException(nameof(fileIndex), "File index must be within the batch.");
            if (completedBytesBeforeCurrentFile < 0)
                throw new ArgumentOutOfRangeException(nameof(completedBytesBeforeCurrentFile), "Completed bytes cannot be negative.");
            if (totalSizeBytes < 0 || completedBytesBeforeCurrentFile > totalSizeBytes)
                throw new ArgumentOutOfRangeException(nameof(totalSizeBytes), "Total size must include the completed bytes and cannot be negative.");

            FileIndex = fileIndex;
            FileCount = fileCount;
            CompletedBytesBeforeCurrentFile = completedBytesBeforeCurrentFile;
            TotalSizeBytes = totalSizeBytes;
        }

        /// <summary>
        /// Creates a progress context for a batch containing one file.
        /// </summary>
        /// <param name="fileSizeBytes">Size of the file in bytes.</param>
        /// <returns>A context with the current file and batch indices set to one.</returns>
        public static UploadBatchProgressContext SingleFile(long fileSizeBytes) => new(1, 1, 0, fileSizeBytes);
    }
}
