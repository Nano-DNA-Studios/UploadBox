using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Models
{
    /// <summary>
    /// Represents a set of browser-selected files submitted for upload.
    /// </summary>
    public sealed class UploadRequest
    {
        /// <summary>
        /// Gets the files included in the request.
        /// </summary>
        public IReadOnlyList<IBrowserFile> Files { get; }

        /// <summary>
        /// Gets the number of files included in the request.
        /// </summary>
        public int FileCount => Files.Count;

        /// <summary>
        /// Initializes a new instance of <see cref="UploadRequest"/>.
        /// </summary>
        /// <param name="files">Files selected for upload.</param>
        public UploadRequest(IReadOnlyList<IBrowserFile> files)
        {
            Files = files;
        }
    }

    /// <summary>
    /// Identifies why a selected file was rejected during validation.
    /// </summary>
    public enum UploadRejectionReason
    {
        /// <summary>
        /// File was not rejected for the upload.
        /// </summary>
        None,
        /// <summary>
        /// All files have been rejected.
        /// </summary>
        EmptySelection,
        /// <summary>
        /// File exceeds configured upload limit.
        /// </summary>
        TooManyFiles,
        /// <summary>
        /// File exceeds the configured size limit.
        /// </summary>
        FileTooLarge,
        /// <summary>
        /// File uses an invalid extension.
        /// </summary>
        InvalidExtension
    }

    /// <summary>
    /// Contains the validation outcome and message for one selected file.
    /// </summary>
    public sealed class UploadFileValidationResult
    {
        /// <summary>
        /// Gets the browser file that was validated.
        /// </summary>
        public IBrowserFile File { get; }

        /// <summary>
        /// Gets the name reported by the browser for the file.
        /// </summary>
        public string FileName => File.Name;

        /// <summary>
        /// Gets the size reported by the browser for the file, in bytes.
        /// </summary>
        public long FileSize => File.Size;

        /// <summary>
        /// Gets the reason the file was rejected, or <see cref="UploadRejectionReason.None"/> when accepted.
        /// </summary>
        public UploadRejectionReason Reason { get; }

        /// <summary>
        /// Gets the validation message associated with the file.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets whether the file's rejection reason is <see cref="UploadRejectionReason.None"/>.
        /// </summary>
        public bool IsAccepted => Reason == UploadRejectionReason.None;

        /// <summary>
        /// Initializes a new instance of <see cref="UploadFileValidationResult"/>.
        /// </summary>
        /// <param name="file">The browser file that was validated.</param>
        /// <param name="reason">The validation outcome for the file.</param>
        /// <param name="message">The message describing the validation outcome.</param>
        public UploadFileValidationResult(IBrowserFile file, UploadRejectionReason reason, string message)
        {
            File = file;
            Reason = reason;
            Message = message;
        }
    }

    /// <summary>
    /// Contains the per-file outcomes and overall decision for an upload selection.
    /// </summary>
    public sealed class UploadValidationResult
    {
        /// <summary>
        /// Gets the validation outcome for each selected file.
        /// </summary>
        public IReadOnlyList<UploadFileValidationResult> Files { get; }

        /// <summary>
        /// Gets whether the selection is blocked from upload.
        /// </summary>
        public bool IsBlocked { get; }

        /// <summary>
        /// Gets whether the selection is not blocked and contains at least one accepted file.
        /// </summary>
        public bool CanUpload => !IsBlocked && Files.Any(file => file.IsAccepted);

        /// <summary>
        /// Gets the message describing the selection validation result.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="UploadValidationResult"/>.
        /// </summary>
        /// <param name="files">Per-file validation outcomes.</param>
        /// <param name="isBlocked">Whether the selection is blocked from upload.</param>
        /// <param name="message">The message describing the selection result.</param>
        public UploadValidationResult(IReadOnlyList<UploadFileValidationResult> files, bool isBlocked, string message)
        {
            Files = files;
            IsBlocked = isBlocked;
            Message = message;
        }
    }

    /// <summary>
    /// Identifies the outcome category for an uploaded file.
    /// </summary>
    public enum UploadFileResultStatus
    {
        /// <summary>
        /// File was uploaded successfully.
        /// </summary>
        Success,
        /// <summary>
        /// File upload was cancelled.
        /// </summary>
        Cancelled,
        /// <summary>
        /// File upload experienced an erorr and failed.
        /// </summary>
        Error
    }

    /// <summary>
    /// Describes the original and saved names and path of an uploaded file.
    /// </summary>
    /// <param name="OriginiOriginalFileName">Name reported for the original browser file.</param>
    /// <param name="SavedFileName">Name used for the saved file.</param>
    /// <param name="SavedPath">Path where the saved file is located.</param>
    public sealed record UploadFile(string OriginiOriginalFileName, string SavedFileName, string SavedPath);

    /// <summary>
    /// Contains byte-count and overwrite information for a completed upload.
    /// </summary>
    /// <param name="BytesWritten">Number of bytes written for the file.</param>
    /// <param name="WasOverwritten">Whether the destination existed when the upload context was created.</param>
    public sealed record UploadFileStats(long BytesWritten, bool WasOverwritten);

    /// <summary>
    /// Describes the temporary-file details and any exception recorded while staging an upload.
    /// </summary>
    /// <param name="TemporaryPath">Path used for the staged temporary file.</param>
    /// <param name="BytesWritten">Number of bytes written to the temporary file.</param>
    /// <param name="Exception">Exception associated with the staging operation, if any.</param>
    public sealed record StagedUploadFile(string TemporaryPath, long BytesWritten, Exception? Exception);

    /// <summary>
    /// Holds the browser file and destination details used during an upload operation.
    /// </summary>
    public sealed class UploadContext
    {
        /// <summary>
        /// Gets the browser file being uploaded.
        /// </summary>
        public IBrowserFile File { get; }

        /// <summary>
        /// Gets the file name stored in this context for the destination.
        /// </summary>
        public string SafeFileName { get; }

        /// <summary>
        /// Gets the destination path for the uploaded file.
        /// </summary>
        public string DestinationPath { get; }

        /// <summary>
        /// Gets whether the destination existed when this context was created.
        /// </summary>
        public bool WasOverwritten { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="UploadContext"/>.
        /// </summary>
        /// <param name="file">The browser file being uploaded.</param>
        /// <param name="safeFileName">The file name to use at the destination.</param>
        /// <param name="destinationPath">The full destination path.</param>
        /// <param name="wasOverwritten">Whether the destination existed at context creation time.</param>
        public UploadContext(IBrowserFile file, string safeFileName, string destinationPath, bool wasOverwritten)
        {
            File = file;
            SafeFileName = safeFileName;
            DestinationPath = destinationPath;
            WasOverwritten = wasOverwritten;
        }

        /// <summary>
        /// Creates an upload context by combining a directory path and file name.
        /// </summary>
        /// <param name="file">The browser file being uploaded.</param>
        /// <param name="safeFileName">The file name to combine with the directory.</param>
        /// <param name="directoryPath">The directory in which the file will be saved.</param>
        /// <returns>An upload context containing the full destination path and whether it currently exists.</returns>
        public static UploadContext NewContext(IBrowserFile file, string safeFileName, string directoryPath)
        {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            string destinationPath = Path.Combine(fullDirectoryPath, safeFileName);
            bool wasOverwritten = System.IO.File.Exists(destinationPath);

            return new UploadContext(file, safeFileName, destinationPath, wasOverwritten);
        }
    }

    /// <summary>
    /// Represents the internal outcome of staging a file before committing it.
    /// </summary>
    internal abstract record StageResult;

    /// <summary>
    /// Represents a successfully staged file.
    /// </summary>
    /// <param name="TemporaryPath">Path to the staged file.</param>
    /// <param name="BytesWritten">Number of bytes written to the staged file.</param>
    internal sealed record StageReady(string TemporaryPath, long BytesWritten) : StageResult;

    /// <summary>
    /// Represents a staging operation that was cancelled.
    /// </summary>
    internal sealed record StageCancelled : StageResult;

    /// <summary>
    /// Represents a staging operation that failed.
    /// </summary>
    /// <param name="Code">Failure code associated with the operation.</param>
    /// <param name="Cause">Exception associated with the failure, if available.</param>
    internal sealed record StageFailed(UploadFailureCode Code, Exception? Cause = null) : StageResult;

    /// <summary>
    /// Represents the outcome of an upload operation for one file.
    /// </summary>
    public abstract record UploadFileResult;

    /// <summary>
    /// Represents a successful upload and its resulting file details and statistics.
    /// </summary>
    /// <param name="File">Details of the saved file.</param>
    /// <param name="Stats">Statistics recorded for the upload.</param>
    public sealed record UploadSucceeded(UploadFile File, UploadFileStats Stats) : UploadFileResult;

    /// <summary>
    /// Represents an upload that was cancelled.
    /// </summary>
    /// <param name="FileName">Name reported for the cancelled file.</param>
    public sealed record UploadCancelled(string FileName) : UploadFileResult;

    /// <summary>
    /// Represents an upload that failed with a code and user-facing message.
    /// </summary>
    /// <param name="FileName">Name reported for the failed file.</param>
    /// <param name="Code">Code identifying the failure.</param>
    /// <param name="UserMessage">Message describing the failure for display.</param>
    public sealed record UploadFailed(string FileName, UploadFailureCode Code, string UserMessage) : UploadFileResult;

    /// <summary>
    /// Identifies a reason an upload operation can fail.
    /// </summary>
    public enum UploadFailureCode
    {
        /// <summary>
        /// File name is invalid.
        /// </summary>
        InvalidFileName,
        /// <summary>
        /// File uses an invalid extensions.
        /// </summary>
        InvalidExtension,
        /// <summary>
        /// File exceeds the configured size limit.
        /// </summary>
        FileTooLarge,
        /// <summary>
        /// Number of bytes read does not match the browser-reported file size. 
        /// </summary>
        SizeMismatch,
        /// <summary>
        /// A failure occurred while transferring the file to a temporary file.
        /// </summary>
        TransferFailed,
        /// <summary>
        /// Destination changed after the upload context was created and could not be committed.
        /// </summary>
        DestinationConflict,
        /// <summary>
        /// The destination directory or file could not be accessed.
        /// </summary>
        DestinationUnavailable
    }
}
