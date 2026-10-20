using Microsoft.AspNetCore.Components.Forms;
using NLog;
using UploadBox.Models;

namespace UploadBox.Services
{
    /// <summary>
    /// Describes the current state of the upload component.
    /// </summary>
    public enum UploadBoxStatus
    {
        /// <summary>
        /// No files are selected and no upload is active.
        /// </summary>
        Idle,

        /// <summary>
        /// One or more files have been selected.
        /// </summary>
        FileSelected,

        /// <summary>
        /// An upload is in progress.
        /// </summary>
        Uploading,

        /// <summary>
        /// An upload completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// An upload was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// An upload encountered an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Validates selected files, stages uploads, and exposes upload state and progress.
    /// </summary>
    public sealed class UploadBoxController
    {
        /// <summary>
        /// Logger used to record upload validation and lifecycle events.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Gets the options used to validate and stage uploads.
        /// </summary>
        public UploadOptions Options { get; }

        /// <summary>
        /// Gets the current status message.
        /// </summary>
        public string StatusMessage { get; private set; }

        /// <summary>
        /// Gets the current upload state.
        /// </summary>
        public UploadBoxStatus Status { get; private set; }

        /// <summary>
        /// Gets the latest progress update, or <see langword="null"/> when no update is available.
        /// </summary>
        public UploadProgress? Progress { get; private set; }

        /// <summary>
        /// Gets whether the controller is currently uploading a file.
        /// </summary>
        public bool IsUploading => Status == UploadBoxStatus.Uploading;

        /// <summary>
        /// Occurs when the controller's observable state changes.
        /// </summary>
        public event EventHandler? StateChanged;

        /// <summary>
        /// Initializes a new instance of <see cref="UploadBoxController"/>.
        /// </summary>
        /// <param name="options">Options to use, or <see langword="null"/> to use default options.</param>
        public UploadBoxController(UploadOptions? options = null)
        {
            Options = options ?? new UploadOptions();

            Status = UploadBoxStatus.Idle;
            StatusMessage = string.Empty;

            Logger.Debug("Initialized upload controller with maximum file size {MaxFileSizeBytes} bytes, maximum file count {MaxFileCount}, and extension {FileExtension}", Options.MaxFileSizeBytes, Options.MaxFileCount, Options.FileExtension);
        }

        /// <summary>
        /// Raises <see cref="StateChanged"/> for the current controller state.
        /// </summary>
        private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Checks whether a file name is blank or equal to a dot or two dots.
        /// </summary>
        private bool IsInvalidFileName(string fileName) => string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == "..";

        /// <summary>
        /// Checks whether a directory path is blank or equal to two dots.
        /// </summary>
        private bool IsInvalidDirectoryPath(string directory) => string.IsNullOrWhiteSpace(directory) || directory == "..";

        /// <summary>
        /// Checks whether the number of bytes written matches the browser-reported file size.
        /// </summary>
        private bool IsValidUploadSize(IBrowserFile file, long bytesWritten) => file.Size == bytesWritten;

        /// <summary>
        /// Checks whether the file extension matches the configured extension, ignoring case.
        /// </summary>
        private bool IsValidExtension(IBrowserFile file)
        {
            string fileExtension = Path.GetExtension(file.Name);

            return string.IsNullOrEmpty(Options.FileExtension) || string.Equals(fileExtension, Options.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the configured policy blocks the selection based on its file outcomes.
        /// </summary>
        private bool IsBlocked(UploadFileValidationResult[] fileResults)
        {
            bool hasRejections = fileResults.Any(file => !file.IsAccepted);
            bool hasAcceptedFiles = fileResults.Any(file => file.IsAccepted);

            return Options.Policy == UploadPolicy.Strict ? hasRejections : !hasAcceptedFiles;
        }

        /// <summary>
        /// Checks the file's validation result and creates a failure result when it is rejected.
        /// </summary>
        /// <param name="file">The browser file to validate.</param>
        /// <param name="fileName">Destination name supplied by the caller; this helper validates the browser file.</param>
        /// <param name="invalidResult">Receives the failure result when validation fails; otherwise receives <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the file passes validation; otherwise, <see langword="false"/>.</returns>
        private bool IsValidUploadFile(IBrowserFile file, string fileName, out UploadFileResult invalidResult)
        {
            UploadFileValidationResult validationResult = GetFileValidationResult(file);

            if (validationResult.IsAccepted)
            {
                invalidResult = null!;
                return true;
            }

            UploadFailureCode code = validationResult.Reason switch
            {
                UploadRejectionReason.InvalidExtension => UploadFailureCode.InvalidExtension,
                UploadRejectionReason.FileTooLarge => UploadFailureCode.FileTooLarge,
                _ => UploadFailureCode.TransferFailed
            };

            invalidResult = CreateFailure(file.Name, code);
            return false;
        }

        /// <summary>
        /// Converts a staging outcome into an upload result when staging did not succeed.
        /// </summary>
        /// <param name="context">Context for the file being uploaded.</param>
        /// <param name="stageResult">Outcome returned by the staging operation.</param>
        /// <param name="result">Receives a cancellation or failure result when staging did not succeed.</param>
        /// <returns><see langword="true"/> when the file was staged successfully.</returns>
        private bool IsStagedSuccess(UploadContext context, StageResult stageResult, out UploadFileResult result)
        {
            if (stageResult is StageReady)
            {
                result = null!;
                return true;
            }

            if (stageResult is StageCancelled)
            {
                result = CreateCancelledResult(context.File.Name);
                return false;
            }

            StageFailed failure = (StageFailed)stageResult;
            result = CreateFailure(context.File.Name, failure.Code);
            return false;
        }

        /// <summary>
        /// Validates each file and rejects otherwise-valid files that exceed the configured count.
        /// </summary>
        private UploadFileValidationResult[] FilterFiles(IReadOnlyList<IBrowserFile> files)
        {
            List<UploadFileValidationResult> fileResults = new List<UploadFileValidationResult>();
            int acceptedCount = 0;

            foreach (IBrowserFile file in files)
            {
                UploadFileValidationResult validationResult = GetFileValidationResult(file);

                if (!validationResult.IsAccepted)
                {
                    fileResults.Add(validationResult);
                    continue;
                }

                if (acceptedCount >= Options.MaxFileCount)
                {
                    string message = $"'{file.Name}' was not selected because the maximum file count is {Options.MaxFileCount}.";
                    fileResults.Add(new UploadFileValidationResult(file, UploadRejectionReason.TooManyFiles, message));
                    continue;
                }

                fileResults.Add(validationResult);
                acceptedCount++;
            }

            return fileResults.ToArray();
        }

        /// <summary>
        /// Determines whether a file meets the configured extension and size limits.
        /// </summary>
        private UploadFileValidationResult GetFileValidationResult(IBrowserFile file)
        {
            if (!IsValidExtension(file))
            {
                string message = $"'{file.Name}' does not use the required extension '{Options.FileExtension}'.";
                return new UploadFileValidationResult(file, UploadRejectionReason.InvalidExtension, message);
            }

            if (file.Size > Options.MaxFileSizeBytes)
            {
                string message = $"'{file.Name}' exceeds the maximum allowed size of {Options.MaxFileSizeBytes} bytes.";
                return new UploadFileValidationResult(file, UploadRejectionReason.FileTooLarge, message);
            }

            return new UploadFileValidationResult(file, UploadRejectionReason.None, string.Empty);
        }

        /// <summary>
        /// Validates a selection and creates per-file outcomes and an overall upload decision.
        /// </summary>
        /// <param name="files">Browser files to validate.</param>
        /// <returns>The per-file validation outcomes, blocking decision, and summary message.</returns>
        public UploadValidationResult ValidateFiles(IReadOnlyList<IBrowserFile> files)
        {
            if (files == null || files.Count == 0)
            {
                Logger.Debug("File selection validation blocked because no files were selected");
                return new UploadValidationResult(Array.Empty<UploadFileValidationResult>(), isBlocked: true, "No files were selected.");
            }

            UploadFileValidationResult[] fileResults = FilterFiles(files);
            bool isBlocked = IsBlocked(fileResults);
            int acceptedCount = fileResults.Count(file => file.IsAccepted);
            string message = CreateValidationMessage(files.Count, acceptedCount, fileResults, isBlocked);

            Logger.Debug("Validated {SelectedFileCount} selected files: {AcceptedFileCount} accepted, {RejectedFileCount} rejected, blocked={IsBlocked}", files.Count, acceptedCount, files.Count - acceptedCount, isBlocked);

            return new UploadValidationResult(fileResults, isBlocked, message);
        }

        /// <summary>
        /// Copies a browser file to a temporary file and reports transfer progress before commit.
        /// </summary>
        /// <param name="context">File and destination information for the upload.</param>
        /// <param name="progressContext">File position and byte offsets for the batch.</param>
        /// <param name="cancellationToken">Token used to cancel the transfer.</param>
        /// <returns>A staging outcome indicating success, cancellation, or failure.</returns>
        private async Task<StageResult> StageUploadFileAsync(UploadContext context, UploadBatchProgressContext progressContext, CancellationToken cancellationToken)
        {
            string? directoryPath = Path.GetDirectoryName(context.DestinationPath);
            bool stagingSucceeded = false;
            long totalRead = 0;

            if (string.IsNullOrEmpty(directoryPath))
            {
                Logger.Debug("Cannot stage upload for {FileName}: destination path has no directory", context.SafeFileName);
                return new StageFailed(UploadFailureCode.DestinationUnavailable);
            }

            string temporaryPath = Path.Combine(directoryPath, $".{context.SafeFileName}.{Guid.NewGuid():N}.uploading");

            Logger.Debug("Staging upload for {FileName} with expected size {FileSizeBytes} bytes", context.SafeFileName, context.File.Size);

            try
            {
                await using Stream uploadStream = context.File.OpenReadStream(Options.MaxFileSizeBytes, cancellationToken);
                await using FileStream temporaryStream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, Options.BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                byte[] buffer = new byte[Options.BufferSize];
                int bytesRead;

                while ((bytesRead = await uploadStream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    totalRead += bytesRead;

                    if (totalRead > Options.MaxFileSizeBytes)
                    {
                        Logger.Debug("Upload for {FileName} exceeded the maximum size of {MaxFileSizeBytes} bytes", context.SafeFileName, Options.MaxFileSizeBytes);
                        return new StageFailed(UploadFailureCode.FileTooLarge);
                    }

                    await temporaryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    ReportProgress(context.SafeFileName, totalRead, context.File.Size, progressContext);
                }

                await temporaryStream.FlushAsync(cancellationToken);

                if (!IsValidUploadSize(context.File, totalRead))
                {
                    Logger.Debug("Upload size mismatch for {FileName}: expected {ExpectedBytes} bytes, received {BytesWritten} bytes", context.SafeFileName, context.File.Size, totalRead);
                    return new StageFailed(UploadFailureCode.SizeMismatch);
                }

                if (context.File.Size == 0)
                    ReportProgress(context.SafeFileName, 0, 0, progressContext);

                stagingSucceeded = true;
                Logger.Debug("Staged upload for {FileName}: {BytesWritten} bytes", context.SafeFileName, totalRead);
                return new StageReady(temporaryPath, totalRead);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.Debug("Upload staging cancelled for {FileName} after {BytesWritten} bytes", context.SafeFileName, totalRead);
                return new StageCancelled();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Upload staging failed for {FileName} after {BytesWritten} bytes", context.SafeFileName, totalRead);
                return new StageFailed(UploadFailureCode.TransferFailed, ex);
            }
            finally
            {
                if (!stagingSucceeded)
                {
                    Logger.Debug("Removing incomplete staged upload for {FileName}", context.SafeFileName);
                    DeleteTemporaryFile(temporaryPath);
                }
            }
        }

        /// <summary>
        /// Moves a staged file into its destination while detecting conflicts and access failures.
        /// </summary>
        /// <param name="context">File and destination information for the upload.</param>
        /// <param name="stagedFile">Temporary file to commit.</param>
        /// <param name="failureCode">Receives the failure code when the file cannot be committed.</param>
        /// <returns><see langword="true"/> when the file is committed; otherwise, <see langword="false"/>.</returns>
        private bool TryCommitTemporaryFile(UploadContext context, StageReady stagedFile, out UploadFailureCode failureCode)
        {
            try
            {
                bool fileExists = File.Exists(context.DestinationPath);

                if (!context.WasOverwritten && fileExists)
                {
                    failureCode = UploadFailureCode.DestinationConflict;
                    Logger.Debug("Cannot commit upload for {FileName}: destination already exists and overwrite was not requested", context.SafeFileName);
                    return false;
                }

                if (fileExists)
                {
                    Logger.Debug("Replacing existing destination for {FileName}", context.SafeFileName);
                    File.Replace(stagedFile.TemporaryPath, context.DestinationPath, destinationBackupFileName: null);
                }
                else
                {
                    Logger.Debug("Creating upload destination for {FileName}", context.SafeFileName);
                    File.Move(stagedFile.TemporaryPath, context.DestinationPath);
                }

                failureCode = default;
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                failureCode = UploadFailureCode.DestinationUnavailable;
                Logger.Error(ex, "Access denied while committing upload for {FileName}", context.SafeFileName);
                return false;
            }
            catch (IOException ex)
            {
                failureCode = File.Exists(context.DestinationPath)
                    ? UploadFailureCode.DestinationConflict
                    : UploadFailureCode.TransferFailed;

                Logger.Error(ex, "I/O error while committing upload for {FileName}; failure code {FailureCode}", context.SafeFileName, failureCode);

                return false;
            }
        }

        /// <summary>
        /// Commits a staged file and deletes its temporary file when the commit fails.
        /// </summary>
        private UploadFileResult CommitValidatedUpload(UploadContext context, StageReady stagedFile)
        {
            bool committed = false;

            try
            {
                if (!TryCommitTemporaryFile(context, stagedFile, out UploadFailureCode failureCode))
                    return CreateFailure(context.File.Name, failureCode);

                committed = true;
                return CreateSuccess(context, stagedFile);
            }
            finally
            {
                if (!committed)
                    DeleteTemporaryFile(stagedFile.TemporaryPath);
            }
        }

        /// <summary>
        /// Uploads a single browser file and reports progress as a one-file batch.
        /// </summary>
        /// <param name="file">Browser file to upload.</param>
        /// <param name="fileName">Name to use at the destination.</param>
        /// <param name="fullDirectoryPath">Directory in which to save the file.</param>
        /// <param name="cancellationToken">Token used to cancel the upload.</param>
        /// <returns>The success, cancellation, or failure result for the upload.</returns>
        public async Task<UploadFileResult> WriteUploadFile(IBrowserFile file, string fileName, string fullDirectoryPath, CancellationToken cancellationToken) => await WriteUploadFile(file, fileName, fullDirectoryPath, UploadBatchProgressContext.SingleFile(file.Size), cancellationToken);

        /// <summary>
        /// Validates, stages, and commits a browser file using the supplied batch progress context.
        /// </summary>
        /// <param name="file">Browser file to upload.</param>
        /// <param name="fileName">Name to use at the destination.</param>
        /// <param name="fullDirectoryPath">Directory in which to save the file.</param>
        /// <param name="progressContext">File position and byte offsets for the batch.</param>
        /// <param name="cancellationToken">Token used to cancel the upload.</param>
        /// <returns>The success, cancellation, or failure result for the upload.</returns>
        /// <exception cref="ArgumentException">Thrown when the batch total does not include the current file's size.</exception>
        public async Task<UploadFileResult> WriteUploadFile(IBrowserFile file, string fileName, string fullDirectoryPath, UploadBatchProgressContext progressContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.Warn("Upload File was cancelled");
                return CreateCancelledResult(file.Name);
            }

            string safeFileName = Path.GetFileName(fileName);

            if (IsInvalidFileName(safeFileName))
            {
                Logger.Error("Upload File has an Invalid File Name");
                return CreateFailure(file.Name, UploadFailureCode.InvalidFileName);
            }

            if (IsInvalidDirectoryPath(fullDirectoryPath))
            {
                Logger.Error("Upload File has an Invalid Directory");
                return CreateFailure(file.Name, UploadFailureCode.DestinationUnavailable);
            }

            if (!IsValidUploadFile(file, safeFileName, out UploadFileResult invalidResult))
            {
                Logger.Error("Upload File has is Invalid");
                return invalidResult;
            }

            if (file.Size > progressContext.TotalSizeBytes - progressContext.CompletedBytesBeforeCurrentFile)
            {
                Logger.Error("Batch progress total {TotalSizeBytes} bytes does not include upload {FileName} of {FileSizeBytes} bytes after {CompletedBytes} completed bytes", progressContext.TotalSizeBytes, file.Name, file.Size, progressContext.CompletedBytesBeforeCurrentFile);
                throw new ArgumentException("The batch total size must include completed bytes and the current file.", nameof(progressContext));
            }

            if (!Directory.Exists(fullDirectoryPath))
            {
                Logger.Debug("Creating upload destination directory for {FileName}", safeFileName);
                Directory.CreateDirectory(fullDirectoryPath);
            }

            UploadContext context = UploadContext.NewContext(file, safeFileName, fullDirectoryPath);
            BeginUpload($"Uploading '{file.Name}'.");
            Logger.Info("Starting upload for {FileName}, size {FileSizeBytes} bytes, batch file {FileIndex} of {FileCount}", file.Name, file.Size, progressContext.FileIndex, progressContext.FileCount);

            StageResult stagedResult = await StageUploadFileAsync(context, progressContext, cancellationToken);

            if (!IsStagedSuccess(context, stagedResult, out UploadFileResult failure))
                return failure;

            return CommitValidatedUpload(context, (StageReady)stagedResult);
        }

        /// <summary>
        /// Updates the controller state and creates an upload failure result.
        /// </summary>
        private UploadFailed CreateFailure(
            string fileName,
            UploadFailureCode code)
        {
            string message = code switch
            {
                UploadFailureCode.InvalidFileName => "The file name is invalid.",
                UploadFailureCode.InvalidExtension => "The file type is not allowed.",
                UploadFailureCode.FileTooLarge => "The file exceeds the allowed size.",
                UploadFailureCode.SizeMismatch => "The uploaded file was incomplete.",
                UploadFailureCode.DestinationConflict => "A file with that name already exists.",
                UploadFailureCode.DestinationUnavailable => "The destination is unavailable.",
                _ => "The file could not be uploaded."
            };

            FailUpload(fileName, code, message);

            return new UploadFailed(fileName, code, message);
        }

        /// <summary>
        /// Updates the controller state and creates a cancellation result.
        /// </summary>
        private UploadCancelled CreateCancelledResult(string fileName)
        {
            string message = $"Upload of '{fileName}' was cancelled.";
            Logger.Info("Upload cancelled for {FileName}", fileName);
            CancelUpload(message);
            return new UploadCancelled(fileName);
        }

        /// <summary>
        /// Creates a success result from a committed staged file and updates the controller state.
        /// </summary>
        private UploadSucceeded CreateSuccess(UploadContext context, StageReady stagedFile)
        {
            UploadFile file = new UploadFile(
                context.File.Name,
                context.SafeFileName,
                context.DestinationPath);

            UploadFileStats stats = new UploadFileStats(stagedFile.BytesWritten, context.WasOverwritten);

            CompleteUpload($"Uploaded '{context.File.Name}'.");
            Logger.Info("Upload completed for {FileName}: {BytesWritten} bytes, overwritten={WasOverwritten}", context.SafeFileName, stagedFile.BytesWritten, context.WasOverwritten);

            return new UploadSucceeded(file, stats);
        }

        /// <summary>
        /// Updates the controller state to reflect the number of files selected.
        /// </summary>
        /// <param name="fileCount">Number of selected files.</param>
        public void MarkFilesSelected(int fileCount)
        {
            Progress = null;

            Logger.Debug("Selection changed: {SelectedFileCount} files selected", fileCount);

            if (fileCount <= 0)
            {
                Status = UploadBoxStatus.Idle;
                StatusMessage = string.Empty;
                NotifyStateChanged();
                return;
            }

            Status = UploadBoxStatus.FileSelected;
            StatusMessage = $"{fileCount} file(s) selected.";
            NotifyStateChanged();
        }

        /// <summary>
        /// Sets the controller state to uploading and publishes the supplied message.
        /// </summary>
        private void BeginUpload(string message = "Uploading...")
        {
            Status = UploadBoxStatus.Uploading;
            StatusMessage = message;
            NotifyStateChanged();
        }

        /// <summary>
        /// Stores the latest progress and publishes the corresponding status message.
        /// </summary>
        private void UpdateProgress(UploadProgress progress)
        {
            StatusMessage = $"Uploading '{progress.FileName}'.";
            Status = UploadBoxStatus.Uploading;
            Progress = progress;
            NotifyStateChanged();
        }

        /// <summary>
        /// Sets the controller state to completed and publishes the supplied message.
        /// </summary>
        private void CompleteUpload(string message = "Upload complete.")
        {
            Status = UploadBoxStatus.Completed;
            StatusMessage = message;
            NotifyStateChanged();
        }

        /// <summary>
        /// Sets the controller state to error and publishes the supplied message.
        /// </summary>
        private void FailUpload(string fileName, UploadFailureCode code, string message)
        {
            if (string.IsNullOrEmpty(message))
                message = string.Empty;

            Logger.Warn("Upload state changed from {PreviousStatus} to {NewStatus} for {FileName} with failure code {FailureCode}", Status, UploadBoxStatus.Error, fileName, code);

            Status = UploadBoxStatus.Error;
            StatusMessage = message;
            NotifyStateChanged();
        }

        /// <summary>
        /// Sets the controller state to cancelled and publishes the supplied message.
        /// </summary>
        private void CancelUpload(string message = "Upload cancelled.")
        {
            Status = UploadBoxStatus.Cancelled;
            StatusMessage = message;
            NotifyStateChanged();
        }

        /// <summary>
        /// Creates and publishes a progress update using the current batch offsets.
        /// </summary>
        private void ReportProgress(string fileName, long bytesTransferred, long fileSizeBytes, UploadBatchProgressContext progressContext)
        {
            UploadProgress update = new(
                fileName,
                progressContext.FileIndex,
                progressContext.FileCount,
                bytesTransferred,
                fileSizeBytes,
                progressContext.CompletedBytesBeforeCurrentFile + bytesTransferred,
                progressContext.TotalSizeBytes);
            UpdateProgress(update);
        }

        /// <summary>
        /// Formats validation messages for rejected files and the overall selection decision.
        /// </summary>
        private string CreateValidationMessage(int selectedCount, int acceptedCount, IReadOnlyList<UploadFileValidationResult> fileResults, bool isBlocked)
        {
            List<string> errors = new List<string>();
            bool hasTooManyFilesError = false;

            foreach (UploadFileValidationResult validationResult in fileResults.Where(file => !file.IsAccepted))
            {
                if (validationResult.Reason == UploadRejectionReason.TooManyFiles)
                {
                    if (hasTooManyFilesError)
                        continue;

                    hasTooManyFilesError = true;
                    errors.Add($"{selectedCount} files were selected, but the maximum allowed is {Options.MaxFileCount}.");
                    continue;
                }

                errors.Add(validationResult.Message);
            }

            if (isBlocked && errors.Count == 0)
                errors.Add("The selection could not be accepted for upload.");

            if (errors.Count == 0)
                return $"{acceptedCount} file(s) are ready to upload.";

            string details = string.Join(Environment.NewLine, errors.Select((error, index) => $"{index + 1}. {error}"));
            string errorLabel = errors.Count == 1 ? "error" : "errors";
            string summary = isBlocked
                ? $"The upload request was rejected. {errors.Count} validation {errorLabel} found."
                : $"{acceptedCount} of {selectedCount} file(s) are ready to upload. " +
                  $"{errors.Count} validation {errorLabel} found. Rejected files will be skipped.";

            return summary + Environment.NewLine + details;
        }

        /// <summary>
        /// Deletes the temporary file when it exists.
        /// </summary>
        private void DeleteTemporaryFile(string temporaryPath)
        {
            if (File.Exists(temporaryPath))
            {
                Logger.Trace("Deleting temporary upload file {TemporaryPath}", temporaryPath);
                File.Delete(temporaryPath);
            }
        }
    }
}
