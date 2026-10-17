using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Models
{
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
}
