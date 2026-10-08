using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Models
{
    public enum UploadRejectionReason
    {
        None,
        EmptySelection,
        TooManyFiles,
        FileTooLarge,
        InvalidExtension
    }

    public enum UploadBoxStatus
    {
        Idle,
        FileSelected,
        Uploading,
        Completed,
        Cancelled,
        Error
    }

    public sealed class UploadRequest
    {
        public IReadOnlyList<IBrowserFile> Files { get; }

        public int FileCount => Files.Count;

        public UploadRequest(IReadOnlyList<IBrowserFile> files)
        {
            Files = files;
        }
    }

    public sealed class UploadFileRejection
    {
        public IBrowserFile File { get; }

        public string FileName => File.Name;

        public long FileSize => File.Size;

        public UploadRejectionReason Reason { get; }

        public string Message { get; }

        public UploadFileRejection(IBrowserFile file, UploadRejectionReason reason, string message)
        {
            File = file;
            Reason = reason;
            Message = message;
        }
    }

    public sealed class UploadValidationResult
    {
        public IReadOnlyList<IBrowserFile> AcceptedFiles { get; }

        public IReadOnlyList<UploadFileRejection> RejectedFiles { get; }

        public IReadOnlyList<UploadRejectionReason> BatchRejectionReasons { get; }

        public bool IsBlocked { get; }

        public bool CanUpload => !IsBlocked && AcceptedFiles.Count > 0;

        public string Message { get; }

        public UploadValidationResult(IReadOnlyList<IBrowserFile> acceptedFiles, IReadOnlyList<UploadFileRejection> rejectedFiles, IReadOnlyList<UploadRejectionReason> batchRejectionReasons, bool isBlocked, string message)
        {
            AcceptedFiles = acceptedFiles;
            RejectedFiles = rejectedFiles;
            BatchRejectionReasons = batchRejectionReasons;
            IsBlocked = isBlocked;
            Message = message;
        }
    }

    public sealed record UploadProgress(string FileName, int FileIndex, int FileCount, long BytesTransferred, long FileSizeBytes, long TotalBytesTransferred, long TotalSizeBytes)
    {
        public double GetFilePercentage(int precision = 2) =>
            UploadProgressMath.CalculatePercentage(BytesTransferred, FileSizeBytes, precision);

        public double GetOverallPercentage(int precision = 2)
        {
            if (FileCount < 1)
                return 0;

            return UploadProgressMath.CalculatePercentage(TotalBytesTransferred, TotalSizeBytes, precision);
        }
    }

    internal static class UploadProgressMath
    {
        public static double CalculatePercentage(long transferredBytes, long totalBytes, int precision)
        {
            if (precision < 0 || precision > 15)
                precision = int.Max(int.Min(15, precision), 0);

            if (totalBytes == 0)
                return 100d;

            if (totalBytes < 0)
                totalBytes = 0;

            double percentage = transferredBytes * 100d / totalBytes;
            percentage = Math.Clamp(percentage, 0d, 100d);

            return Math.Round(percentage, precision, MidpointRounding.AwayFromZero);

        }
    }

    public enum UploadFileResultStatus
    {
        Success,
        Cancelled,
        Error
    }

    public sealed record UploadFile(string OriginiOriginalFileName, string SavedFileName, string SavedPath);

    public sealed record UploadFileStats(long BytesWritten, bool WasOverwritten);

    public sealed record UploadFileResult(UploadFile UploadFile, UploadFileResultStatus Status, UploadFileStats Stats, string? ErrorMessage = null)
    {
        public bool IsCancelled => Status == UploadFileResultStatus.Cancelled;

        public bool IsSuccess => Status == UploadFileResultStatus.Success;

        public bool IsError => Status == UploadFileResultStatus.Error;
    }

    public sealed record StagedUploadFile(string TemporaryPath, long BytesWritten, Exception? Exception);

    public sealed class UploadContext
    {
        public IBrowserFile File { get; }

        public string SafeFileName { get; }

        public string DestinationPath { get; }

        public bool WasOverwritten { get; }

        public StagedUploadFile? StagedUpload { get; set; }

        public bool WasCommitted { get; set; }

        public UploadContext(IBrowserFile file, string safeFileName, string destinationPath, bool wasOverwritten)
        {
            File = file;
            SafeFileName = safeFileName;
            DestinationPath = destinationPath;
            WasOverwritten = wasOverwritten;
        }
    }
}
