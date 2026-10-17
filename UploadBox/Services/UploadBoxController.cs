using Microsoft.AspNetCore.Components.Forms;
using UploadBox.Models;

namespace UploadBox.Services
{
    public enum UploadBoxStatus
    {
        Idle,
        FileSelected,
        Uploading,
        Completed,
        Cancelled,
        Error
    }

    public sealed class UploadBoxController
    {
        public UploadOptions Options { get; }

        public string StatusMessage { get; private set; }

        public UploadBoxStatus Status { get; private set; }

        public UploadProgress? Progress { get; private set; }

        public bool IsUploading => Status == UploadBoxStatus.Uploading;

        public event EventHandler? StateChanged;

        public UploadBoxController(UploadOptions? options = null)
        {
            Options = options ?? new UploadOptions();

            Status = UploadBoxStatus.Idle;
            StatusMessage = string.Empty;
        }

        private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        private bool IsInvalidFileName(string fileName) => string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == "..";

        private bool IsInvalidDirectoryPath(string directory) => string.IsNullOrWhiteSpace(directory) || directory == "..";

        private bool IsValidUploadSize(IBrowserFile file, long bytesWritten) => file.Size == bytesWritten;

        private bool IsValidExtension(IBrowserFile file)
        {
            string fileExtension = Path.GetExtension(file.Name);

            return string.IsNullOrEmpty(Options.FileExtension) || string.Equals(fileExtension, Options.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsBlocked(UploadFileValidationResult[] fileResults)
        {
            bool hasRejections = fileResults.Any(file => !file.IsAccepted);
            bool hasAcceptedFiles = fileResults.Any(file => file.IsAccepted);

            return Options.Policy == UploadPolicy.Strict ? hasRejections : !hasAcceptedFiles;
        }

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

        public UploadValidationResult ValidateFiles(IReadOnlyList<IBrowserFile> files)
        {
            if (files == null || files.Count == 0)
                return new UploadValidationResult(Array.Empty<UploadFileValidationResult>(), isBlocked: true, "No files were selected.");

            UploadFileValidationResult[] fileResults = FilterFiles(files);
            bool isBlocked = IsBlocked(fileResults);
            int acceptedCount = fileResults.Count(file => file.IsAccepted);
            string message = CreateValidationMessage(files.Count, acceptedCount, fileResults, isBlocked);

            return new UploadValidationResult(fileResults, isBlocked, message);
        }

        private async Task<StageResult> StageUploadFileAsync(UploadContext context, UploadBatchProgressContext progressContext, CancellationToken cancellationToken)
        {
            string? directoryPath = Path.GetDirectoryName(context.DestinationPath);
            bool stagingSucceeded = false;
            long totalRead = 0;

            if (string.IsNullOrEmpty(directoryPath))
                return new StageFailed(UploadFailureCode.DestinationUnavailable);

            string temporaryPath = Path.Combine(directoryPath, $".{context.SafeFileName}.{Guid.NewGuid():N}.uploading");

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
                        return new StageFailed(UploadFailureCode.FileTooLarge);

                    await temporaryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    ReportProgress(context.SafeFileName, totalRead, context.File.Size, progressContext);
                }

                await temporaryStream.FlushAsync(cancellationToken);

                if (!IsValidUploadSize(context.File, totalRead))
                    return new StageFailed(UploadFailureCode.SizeMismatch);

                if (context.File.Size == 0)
                    ReportProgress(context.SafeFileName, 0, 0, progressContext);

                stagingSucceeded = true;
                return new StageReady(temporaryPath, totalRead);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new StageCancelled();
            }
            catch (Exception ex)
            {
                return new StageFailed(UploadFailureCode.TransferFailed, ex);
            }
            finally
            {
                if (!stagingSucceeded)
                    DeleteTemporaryFile(temporaryPath);
            }
        }

        private bool TryCommitTemporaryFile(UploadContext context, StageReady stagedFile, out UploadFailureCode failureCode)
        {
            try
            {
                bool fileExists = File.Exists(context.DestinationPath);

                if (!context.WasOverwritten && fileExists)
                {
                    failureCode = UploadFailureCode.DestinationConflict;
                    return false;
                }

                if (fileExists)
                    File.Replace(stagedFile.TemporaryPath, context.DestinationPath, destinationBackupFileName: null);
                else
                    File.Move(stagedFile.TemporaryPath, context.DestinationPath);

                failureCode = default;
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                failureCode = UploadFailureCode.DestinationUnavailable;
                return false;
            }
            catch (IOException)
            {
                failureCode = File.Exists(context.DestinationPath)
                    ? UploadFailureCode.DestinationConflict
                    : UploadFailureCode.TransferFailed;

                return false;
            }
        }

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

        public async Task<UploadFileResult> WriteUploadFile(IBrowserFile file, string fileName, string fullDirectoryPath, CancellationToken cancellationToken) => await WriteUploadFile(file, fileName, fullDirectoryPath, UploadBatchProgressContext.SingleFile(file.Size), cancellationToken);

        public async Task<UploadFileResult> WriteUploadFile(IBrowserFile file, string fileName, string fullDirectoryPath, UploadBatchProgressContext progressContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCancelledResult(file.Name);

            string safeFileName = Path.GetFileName(fileName);

            if (IsInvalidFileName(safeFileName))
                return CreateFailure(file.Name, UploadFailureCode.InvalidFileName);

            if (IsInvalidDirectoryPath(fullDirectoryPath))
                return CreateFailure(file.Name, UploadFailureCode.DestinationUnavailable);

            if (!IsValidUploadFile(file, safeFileName, out UploadFileResult invalidResult))
                return invalidResult;

            if (file.Size > progressContext.TotalSizeBytes - progressContext.CompletedBytesBeforeCurrentFile)
                throw new ArgumentException("The batch total size must include completed bytes and the current file.", nameof(progressContext));

            if (!Directory.Exists(fullDirectoryPath))
                Directory.CreateDirectory(fullDirectoryPath);

            UploadContext context = UploadContext.NewContext(file, safeFileName, fullDirectoryPath);
            BeginUpload($"Uploading '{file.Name}'.");

            StageResult stagedResult = await StageUploadFileAsync(context, progressContext, cancellationToken);

            if (!IsStagedSuccess(context, stagedResult, out UploadFileResult failure))
                return failure;

            return CommitValidatedUpload(context, (StageReady)stagedResult);
        }

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

            FailUpload(message);

            return new UploadFailed(fileName, code, message);
        }

        private UploadCancelled CreateCancelledResult(string fileName)
        {
            string message = $"Upload of '{fileName}' was cancelled.";
            CancelUpload(message);
            return new UploadCancelled(fileName);
        }

        private UploadSucceeded CreateSuccess(UploadContext context, StageReady stagedFile)
        {
            UploadFile file = new UploadFile(
                context.File.Name,
                context.SafeFileName,
                context.DestinationPath);

            UploadFileStats stats = new UploadFileStats(stagedFile.BytesWritten, context.WasOverwritten);

            CompleteUpload($"Uploaded '{context.File.Name}'.");

            return new UploadSucceeded(file, stats);
        }

        public void MarkFilesSelected(int fileCount)
        {
            Progress = null;

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

        private void BeginUpload(string message = "Uploading...")
        {
            Status = UploadBoxStatus.Uploading;
            StatusMessage = message;
            NotifyStateChanged();
        }

        private void UpdateProgress(UploadProgress progress)
        {
            StatusMessage = $"Uploading '{progress.FileName}'.";
            Status = UploadBoxStatus.Uploading;
            Progress = progress;
            NotifyStateChanged();
        }

        private void CompleteUpload(string message = "Upload complete.")
        {
            Status = UploadBoxStatus.Completed;
            StatusMessage = message;
            NotifyStateChanged();
        }

        private void FailUpload(string message)
        {
            if (string.IsNullOrEmpty(message))
                message = string.Empty;

            Status = UploadBoxStatus.Error;
            StatusMessage = message;
            NotifyStateChanged();
        }

        private void CancelUpload(string message = "Upload cancelled.")
        {
            Status = UploadBoxStatus.Cancelled;
            StatusMessage = message;
            NotifyStateChanged();
        }

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

        private void DeleteTemporaryFile(string temporaryPath)
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
