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
}
