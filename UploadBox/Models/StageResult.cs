
namespace UploadBox.Models
{
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
}
