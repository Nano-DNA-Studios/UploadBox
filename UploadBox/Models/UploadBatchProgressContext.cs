namespace UploadBox.Models
{
    public sealed record UploadBatchProgressContext
    {
        public int FileIndex { get; }

        public int FileCount { get; }

        public long CompletedBytesBeforeCurrentFile { get; }

        public long TotalSizeBytes { get; }

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

        public static UploadBatchProgressContext SingleFile(long fileSizeBytes) => new(1, 1, 0, fileSizeBytes);
    }
}
