using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using global::UploadBox.Models;
using global::UploadBox.Services;
using global::UploadBox.Pages;
using UploadBox.Tests.UI;

namespace UploadBox.Tests.UI.Components;

/// <summary>
/// Coordinates validation, upload processing, cancellation, and display state for the playground.
/// </summary>
public partial class UploadPlayground : IDisposable
{
    private const long MaxFileSizeBytes = 512L * 1024 * 1024;
    private const int MaxBatchFileCount = 20;
    private const int UploadBufferSize = 128 * 1024;

    /// <summary>
    /// Gets or sets whether the playground accepts a multiple-file selection.
    /// </summary>
    [Parameter]
    public bool AllowMultipleFiles { get; set; }

    private UploadBoxController Controller { get; set; } = null!;
    private CancellationTokenSource? _uploadCancellation;
    private readonly List<UploadDisplayItem> _uploadItems = [];
    private readonly List<ActivityMessage> _messages = [];
    private bool _isProcessing;
    private bool _cancelRequested;
    private string? _currentFileName;
    private UploadDisplayItem? _currentUploadItem;
    private long _completedBytes;
    private long _batchTotalBytes;
    private double SelectedUploadProgress;

    private string UploadDirectory => Path.Combine(AppContext.BaseDirectory, "Upload");
    private string MaxFileSizeLabel => "512 MiB";

    private string StatusText => _cancelRequested
        ? "Cancellation requested"
        : _isProcessing
            ? "Uploading"
            : Controller.Status switch
            {
                UploadBoxStatus.Idle => "Ready for files",
                UploadBoxStatus.FileSelected => Controller.StatusMessage,
                UploadBoxStatus.Completed => "Upload complete",
                UploadBoxStatus.Cancelled => "Upload cancelled",
                UploadBoxStatus.Error => "Upload error",
                _ => Controller.StatusMessage
            };

    private string StatusClass => _cancelRequested
        ? "status-cancelled"
        : _isProcessing
            ? "status-active"
            : Controller.Status switch
            {
                UploadBoxStatus.Completed => "status-success",
                UploadBoxStatus.Cancelled => "status-cancelled",
                UploadBoxStatus.Error => "status-error",
                _ => "status-idle"
            };

    /// <summary>
    /// Creates the controller using the current single-file or batch settings and subscribes to state changes.
    /// </summary>
    protected override void OnInitialized()
    {
        UploadOptions options = new(
            maxFileCount: AllowMultipleFiles ? MaxBatchFileCount : 1,
            maxFileSizeBytes: MaxFileSizeBytes,
            policy: AllowMultipleFiles ? UploadPolicy.SkipInvalidFiles : UploadPolicy.Strict,
            bufferSize: UploadBufferSize);

        Controller = new UploadBoxController(options);
        Controller.StateChanged += HandleControllerStateChanged;
    }

    /// <summary>
    /// Validates the selection, uploads accepted files sequentially, and records their outcomes.
    /// </summary>
    /// <param name="request">Selected files to validate and process.</param>
    private async Task HandleUploadRequested(UploadRequest request)
    {
        if (_isProcessing)
        {
            AddMessage("A batch is already running; the new selection was ignored.", "warning");
            return;
        }

        _isProcessing = true;
        _cancelRequested = false;
        _uploadCancellation = new CancellationTokenSource();
        _uploadItems.Clear();
        _currentFileName = null;
        _currentUploadItem = null;
        _completedBytes = 0;
        _batchTotalBytes = 0;
        SelectedUploadProgress = 0;

        try
        {
            AddMessage($"Received a selection of {request.FileCount} file(s).", "info");
            UploadValidationResult validation = Controller.ValidateFiles(request.Files);
            AddMessage(validation.Message, validation.IsBlocked ? "error" : "info");

            foreach (UploadFileValidationResult fileResult in validation.Files)
            {
                string status = fileResult.IsAccepted
                    ? validation.IsBlocked ? "Blocked" : "Queued"
                    : "Rejected";
                string message = fileResult.IsAccepted
                    ? validation.IsBlocked ? "The strict upload policy blocked this selection." : "Waiting to upload."
                    : fileResult.Message;

                _uploadItems.Add(new UploadDisplayItem(fileResult.File, fileResult.FileName, fileResult.FileSize, status, message));
            }

            if (!validation.CanUpload)
            {
                if (_uploadItems.Count == 0)
                    AddMessage("No files are available to upload.", "warning");
                return;
            }

            List<UploadFileValidationResult> acceptedFiles = validation.Files.Where(file => file.IsAccepted).ToList();
            _batchTotalBytes = acceptedFiles.Sum(file => file.FileSize);
            Directory.CreateDirectory(UploadDirectory);
            AddMessage($"Saving accepted files to {UploadDirectory}.", "info");

            for (int index = 0; index < acceptedFiles.Count; index++)
            {
                UploadFileValidationResult fileResult = acceptedFiles[index];
                UploadDisplayItem item = _uploadItems.First(entry => ReferenceEquals(entry.File, fileResult.File));

                if (_uploadCancellation.Token.IsCancellationRequested)
                {
                    MarkRemainingFilesSkipped(acceptedFiles, index);
                    break;
                }

                _currentFileName = fileResult.FileName;
                _currentUploadItem = item;
                item.SetProgress(new UploadProgress(fileResult.FileName, index + 1, acceptedFiles.Count, 0, fileResult.FileSize, _completedBytes, _batchTotalBytes));
                SelectedUploadProgress = item.Progress!.GetOverallPercentage();
                item.SetStatus("Uploading", "Transfer in progress.");
                AddMessage($"Uploading '{fileResult.FileName}' ({FormatBytes(fileResult.FileSize)}).", "info");

                Controller.MarkFilesSelected(acceptedFiles.Count);
                await InvokeAsync(StateHasChanged);

                UploadFileResult result;
                try
                {
                    result = await Controller.WriteUploadFile(
                        fileResult.File,
                        Path.GetFileName(fileResult.FileName),
                        UploadDirectory,
                        new UploadBatchProgressContext(index + 1, acceptedFiles.Count, _completedBytes, _batchTotalBytes),
                        _uploadCancellation.Token);
                }
                catch (OperationCanceledException) when (_uploadCancellation.Token.IsCancellationRequested)
                {
                    item.SetStatus("Cancelled", "The active transfer was cancelled.");
                    AddMessage($"Upload of '{fileResult.FileName}' was cancelled.", "warning");
                    MarkRemainingFilesSkipped(acceptedFiles, index + 1);
                    break;
                }
                catch (Exception exception)
                {
                    item.SetStatus("Failed", exception.Message);
                    AddMessage($"Failed to upload '{fileResult.FileName}': {exception.Message}", "error");
                    continue;
                }

                if (result is UploadSucceeded success)
                {
                    item.SetStatus("Uploaded", $"Saved {FormatBytes(success.Stats.BytesWritten)}.");
                    _completedBytes += success.Stats.BytesWritten;
                    item.SetProgress(new UploadProgress(fileResult.FileName, index + 1, acceptedFiles.Count, success.Stats.BytesWritten, fileResult.FileSize, _completedBytes, _batchTotalBytes));
                    AddMessage($"Uploaded '{fileResult.FileName}' to '{success.File.SavedPath}'.", "success");
                    continue;
                }

                if (result is UploadCancelled)
                {
                    item.SetStatus("Cancelled", "The active transfer was cancelled.");
                    AddMessage($"Upload of '{fileResult.FileName}' was cancelled.", "warning");
                    MarkRemainingFilesSkipped(acceptedFiles, index + 1);
                    break;
                }

                UploadFailed failure = (UploadFailed)result;
                item.SetStatus("Failed", failure.UserMessage);
                AddMessage($"Failed to upload '{fileResult.FileName}': {failure.UserMessage}", "error");
            }

            if (_cancelRequested)
                AddMessage("Batch stopped. Files completed before cancellation remain saved.", "warning");
            else if (_uploadItems.Any(item => item.Status == "Failed"))
                AddMessage("Batch finished with one or more failed files.", "warning");
            else
                AddMessage("Batch processing finished.", "success");
        }
        catch (Exception exception)
        {
            AddMessage($"The upload page encountered an error: {exception.Message}", "error");
        }
        finally
        {
            _isProcessing = false;
            _currentFileName = null;
            _currentUploadItem = null;
            _uploadCancellation?.Dispose();
            _uploadCancellation = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Requests cancellation of the active upload batch.
    /// </summary>
    private void CancelUpload()
    {
        if (!_isProcessing || _uploadCancellation is null || _cancelRequested)
            return;

        _cancelRequested = true;
        AddMessage("Cancellation requested. Waiting for the active transfer to stop…", "warning");
        _uploadCancellation.Cancel();
    }

    private void MarkRemainingFilesSkipped(IReadOnlyList<UploadFileValidationResult> acceptedFiles, int startIndex)
    {
        for (int index = startIndex; index < acceptedFiles.Count; index++)
        {
            UploadDisplayItem item = _uploadItems.First(entry => ReferenceEquals(entry.File, acceptedFiles[index].File));
            item.SetStatus("Skipped", "Not started because the batch was cancelled.");
        }
    }

    /// <summary>
    /// Applies controller progress updates to the active file and refreshes the component.
    /// </summary>
    private void HandleControllerStateChanged(object? sender, EventArgs eventArgs)
    {
        UploadProgress? progress = Controller.Progress;

        _ = InvokeAsync(() =>
        {
            if (_isProcessing
                && progress is not null
                && _currentUploadItem is not null
                && progress.FileIndex == _currentUploadItem.Progress?.FileIndex
                && string.Equals(Path.GetFileName(progress.FileName), Path.GetFileName(_currentFileName), StringComparison.OrdinalIgnoreCase))
            {
                _currentUploadItem.SetProgress(progress);
                SelectedUploadProgress = progress.GetOverallPercentage();
            }

            StateHasChanged();
        });
    }

    private void AddMessage(string message, string kind)
    {
        _messages.Add(new ActivityMessage(DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture), message, kind));
    }

    private static string FormatBytes(long bytes) => UploadPlaygroundFormatting.FormatBytes(bytes);

    /// <summary>
    /// Unsubscribes from controller updates and cancels any active transfer.
    /// </summary>
    public void Dispose()
    {
        if (Controller is not null)
            Controller.StateChanged -= HandleControllerStateChanged;

        _uploadCancellation?.Cancel();
        _uploadCancellation?.Dispose();
    }

}


