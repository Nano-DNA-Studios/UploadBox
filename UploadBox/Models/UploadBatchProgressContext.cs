namespace UploadBox.Models
{
    /// <summary>Describes this file's position and byte range within a batch upload.</summary>
    public sealed record UploadBatchProgressContext
    {
        /// <summary>Gets the current file's one-based index within the batch.</summary>
        public int FileIndex { get; }

        /// <summary>Gets the number of files in the batch.</summary>
        public int FileCount { get; }

        /// <summary>Gets the bytes successfully completed before the current file began.</summary>
        public long CompletedBytesBeforeCurrentFile { get; }

        /// <summary>Gets the total size, in bytes, of all files in the batch.</summary>
        public long TotalSizeBytes { get; }

        /// <summary>Creates a context for reporting the current file's progress within a batch.</summary>
        /// <param name="fileIndex">The current file's one-based position.</param>
        /// <param name="fileCount">The number of files in the batch.</param>
        /// <param name="completedBytesBeforeCurrentFile">The bytes completed before this file started.</param>
        /// <param name="totalSizeBytes">The total size of all files in the batch.</param>
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

        /// <summary>Creates a progress context for a standalone file upload.</summary>
        /// <param name="fileSizeBytes">The size of the file being uploaded.</param>
        public static UploadBatchProgressContext SingleFile(long fileSizeBytes) => new(1, 1, 0, fileSizeBytes);
    }
}
