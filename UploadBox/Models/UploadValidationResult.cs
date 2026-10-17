
namespace UploadBox.Models
{
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
}
