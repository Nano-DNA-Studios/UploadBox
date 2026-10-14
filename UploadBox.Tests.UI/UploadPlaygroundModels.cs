using Microsoft.AspNetCore.Components.Forms;
using UploadBox.Models;

namespace UploadBox.Tests.UI;

public sealed class UploadDisplayItem(IBrowserFile file, string fileName, long fileSize, string status, string message)
{
    public IBrowserFile File { get; } = file;
    public string FileName { get; } = fileName;
    public long FileSize { get; } = fileSize;
    public string Status { get; private set; } = status;
    public string Message { get; private set; } = message;
    public UploadProgress? Progress { get; private set; }
    public long BytesTransferred => Progress?.BytesTransferred ?? 0;
    public double FilePercentage => Progress?.GetFilePercentage() ?? 0;

    public string StatusClass => Status.ToLowerInvariant() switch
    {
        "uploaded" => "result-success",
        "failed" or "rejected" or "blocked" => "result-error",
        "cancelled" or "skipped" => "result-cancelled",
        "uploading" => "result-active",
        _ => "result-idle"
    };

    public string StatusIcon => Status switch
    {
        "Uploaded" => "✓",
        "Failed" or "Rejected" or "Blocked" => "!",
        "Cancelled" or "Skipped" => "–",
        "Uploading" => "↗",
        _ => "•"
    };

    public void SetStatus(string newStatus, string newMessage)
    {
        Status = newStatus;
        Message = newMessage;
    }

    public void SetProgress(UploadProgress progress) => Progress = progress;
}

public sealed record ActivityMessage(string Timestamp, string Text, string KindClass);

internal static class UploadPlaygroundFormatting
{
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
