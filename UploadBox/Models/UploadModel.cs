using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Models
{
    public sealed class UploadRequest
    {
        public IReadOnlyList<IBrowserFile> Files { get; }

        public int FileCount => Files.Count;

        public UploadRequest(IReadOnlyList<IBrowserFile> files)
        {
            Files = files;
        }
    }

    public enum UploadRejectionReason
    {
        None,
        EmptySelection,
        TooManyFiles,
        FileTooLarge,
        InvalidExtension
    }

    public sealed class UploadFileValidationResult
    {
        public IBrowserFile File { get; }

        public string FileName => File.Name;

        public long FileSize => File.Size;

        public UploadRejectionReason Reason { get; }

        public string Message { get; }

        public bool IsAccepted => Reason == UploadRejectionReason.None;

        public UploadFileValidationResult(IBrowserFile file, UploadRejectionReason reason, string message)
        {
            File = file;
            Reason = reason;
            Message = message;
        }
    }

    public sealed class UploadValidationResult
    {
        public IReadOnlyList<UploadFileValidationResult> Files { get; }

        public bool IsBlocked { get; }

        public bool CanUpload => !IsBlocked && Files.Any(file => file.IsAccepted);

        public string Message { get; }

        public UploadValidationResult(IReadOnlyList<UploadFileValidationResult> files, bool isBlocked, string message)
        {
            Files = files;
            IsBlocked = isBlocked;
            Message = message;
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

    public sealed record StagedUploadFile(string TemporaryPath, long BytesWritten, Exception? Exception);

    public sealed class UploadContext
    {
        public IBrowserFile File { get; }

        public string SafeFileName { get; }

        public string DestinationPath { get; }

        public bool WasOverwritten { get; }

        public UploadContext(IBrowserFile file, string safeFileName, string destinationPath, bool wasOverwritten)
        {
            File = file;
            SafeFileName = safeFileName;
            DestinationPath = destinationPath;
            WasOverwritten = wasOverwritten;
        }

        public static UploadContext NewContext(IBrowserFile file, string safeFileName, string directoryPath)
        {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            string destinationPath = Path.Combine(fullDirectoryPath, safeFileName);
            bool wasOverwritten = System.IO.File.Exists(destinationPath);

            return new UploadContext(file, safeFileName, destinationPath, wasOverwritten);
        }
    }

    internal abstract record StageResult;

    internal sealed record StageReady(string TemporaryPath, long BytesWritten) : StageResult;

    internal sealed record StageCancelled : StageResult;

    internal sealed record StageFailed(UploadFailureCode Code, Exception? Cause = null) : StageResult;

    public abstract record UploadFileResult;

    public sealed record UploadSucceeded(UploadFile File, UploadFileStats Stats) : UploadFileResult;

    public sealed record UploadCancelled(string FileName) : UploadFileResult;

    public sealed record UploadFailed(string FileName, UploadFailureCode Code, string UserMessage) : UploadFileResult;

    public enum UploadFailureCode
    {
        InvalidFileName,
        InvalidExtension,
        FileTooLarge,
        SizeMismatch,
        TransferFailed,
        DestinationConflict,
        DestinationUnavailable
    }
}
