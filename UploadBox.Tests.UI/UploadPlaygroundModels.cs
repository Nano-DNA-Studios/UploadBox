using Microsoft.AspNetCore.Components.Forms;
using UploadBox.Models;

namespace UploadBox.Tests.UI;

/// <summary>
/// Stores the file, display state, and progress shown for one upload.
/// </summary>
public sealed class UploadDisplayItem(IBrowserFile file, string fileName, long fileSize, string status, string message)
{
    /// <summary>
    /// Gets the selected browser file.
    /// </summary>
    public IBrowserFile File { get; } = file;

    /// <summary>
    /// Gets the file name displayed in the results.
    /// </summary>
    public string FileName { get; } = fileName;

    /// <summary>
    /// Gets the declared file size in bytes.
    /// </summary>
    public long FileSize { get; } = fileSize;

    /// <summary>
    /// Gets the current display status for the file.
    /// </summary>
    public string Status { get; private set; } = status;

    /// <summary>
    /// Gets the message associated with the current status.
    /// </summary>
    public string Message { get; private set; } = message;

    /// <summary>
    /// Gets the most recent upload progress, when available.
    /// </summary>
    public UploadProgress? Progress { get; private set; }

    /// <summary>
    /// Gets the transferred byte count, or zero before progress is available.
    /// </summary>
    public long BytesTransferred => Progress?.BytesTransferred ?? 0;

    /// <summary>
    /// Gets the file completion percentage, or zero before progress is available.
    /// </summary>
    public double FilePercentage => Progress?.GetFilePercentage() ?? 0;

    /// <summary>
    /// Gets the CSS class associated with the current status.
    /// </summary>
    public string StatusClass => Status.ToLowerInvariant() switch
    {
        "uploaded" => "result-success",
        "failed" or "rejected" or "blocked" => "result-error",
        "cancelled" or "skipped" => "result-cancelled",
        "uploading" => "result-active",
        _ => "result-idle"
    };

    /// <summary>
    /// Gets the symbol associated with the current status.
    /// </summary>
    public string StatusIcon => Status switch
    {
        "Uploaded" => "✓",
        "Failed" or "Rejected" or "Blocked" => "!",
        "Cancelled" or "Skipped" => "–",
        "Uploading" => "↗",
        _ => "•"
    };

    /// <summary>
    /// Updates the displayed status and message for the file.
    /// </summary>
    /// <param name="newStatus">Status label to display.</param>
    /// <param name="newMessage">Message associated with the status.</param>
    public void SetStatus(string newStatus, string newMessage)
    {
        Status = newStatus;
        Message = newMessage;
    }

    /// <summary>
    /// Updates the displayed progress for the file.
    /// </summary>
    /// <param name="progress">Progress values to display.</param>
    public void SetProgress(UploadProgress progress) => Progress = progress;
}

/// <summary>
/// Represents a timestamped message in the playground activity log.
/// </summary>
public sealed record ActivityMessage(string Timestamp, string Text, string KindClass);

/// <summary>
/// Formats values for display in the upload playground.
/// </summary>
internal static class UploadPlaygroundFormatting
{
    /// <summary>
    /// Converts a byte count to a compact binary-unit label.
    /// </summary>
    /// <param name="bytes">Number of bytes to format.</param>
    /// <returns>The value expressed in B, KB, MB, or GB.</returns>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int sizeIndex = 0;
        while (value >= 1024 && sizeIndex < sizes.Length - 1)
        {
            value /= 1024;
            sizeIndex++;
        }

        return $"{value:0.##} {sizes[sizeIndex]}";
    }
}
