
namespace UploadBox.Models
{
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
}
