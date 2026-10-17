
namespace UploadBox.Models
{
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
}
