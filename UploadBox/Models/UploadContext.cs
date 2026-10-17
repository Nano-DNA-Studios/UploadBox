using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Models
{
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
}
