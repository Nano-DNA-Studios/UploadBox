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

        private bool IsTooManyFiles(bool hasBatchFileCountError, UploadFileRejection rejection) => hasBatchFileCountError && rejection.Reason == UploadRejectionReason.TooManyFiles;

        private bool IsValidExtension(IBrowserFile file)
        {
            string fileExtension = Path.GetExtension(file.Name);

            return string.IsNullOrEmpty(Options.FileExtension) || string.Equals(fileExtension, Options.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsBlocked(List<IBrowserFile> validFiles, List<UploadFileRejection> rejectedFiles, List<UploadRejectionReason> batchRejectionReasons)
        {
            bool hasRejections = rejectedFiles.Count > 0 || batchRejectionReasons.Count > 0;

            return Options.Policy == UploadPolicy.Strict ? hasRejections : validFiles.Count == 0;
        }

        private void FilterFiles(IReadOnlyList<IBrowserFile> files, List<IBrowserFile> validFiles, List<UploadFileRejection> rejectedFiles)
        {
            foreach (IBrowserFile file in files)
            {
                UploadFileRejection? rejection = GetFileRejection(file);

                if (rejection is not null)
                {
                    rejectedFiles.Add(rejection);
                    continue;
                }

                if (validFiles.Count >= Options.MaxFileCount)
                {
                    string message = $"'{file.Name}' was not selected because the maximum file count is {Options.MaxFileCount}.";
                    rejectedFiles.Add(new UploadFileRejection(file, UploadRejectionReason.TooManyFiles, message));
                    continue;
                }

                validFiles.Add(file);
            }
        }

        private UploadFileRejection? GetFileRejection(IBrowserFile file)
        {
            if (!IsValidExtension(file))
            {
                string message = $"'{file.Name}' does not use the required extension '{Options.FileExtension}'.";

                return new UploadFileRejection(file, UploadRejectionReason.InvalidExtension, message);
            }

            if (file.Size > Options.MaxFileSizeBytes)
            {
                string message = $"'{file.Name}' exceeds the maximum allowed size of {Options.MaxFileSizeBytes} bytes.";

                return new UploadFileRejection(file, UploadRejectionReason.FileTooLarge, message);
            }

            return null;
        }

        public UploadValidationResult ValidateFiles(IReadOnlyList<IBrowserFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return new UploadValidationResult(
                    Array.Empty<IBrowserFile>(),
                    Array.Empty<UploadFileRejection>(),
                    new[] { UploadRejectionReason.EmptySelection },
                    isBlocked: true,
                    "No files were selected.");
            }

            List<IBrowserFile> validFiles = new(files.Count);
            List<UploadFileRejection> rejectedFiles = new List<UploadFileRejection>();
            List<UploadRejectionReason> batchRejectionReasons = new List<UploadRejectionReason>();

            FilterFiles(files, validFiles, rejectedFiles);

            if (files.Count > Options.MaxFileCount)
                batchRejectionReasons.Add(UploadRejectionReason.TooManyFiles);

            bool isBlocked = IsBlocked(validFiles, rejectedFiles, batchRejectionReasons);

            IReadOnlyList<IBrowserFile> acceptedFiles = isBlocked ? Array.Empty<IBrowserFile>() : validFiles.ToArray();

            string message = CreateValidationMessage(files.Count, acceptedFiles.Count, rejectedFiles, batchRejectionReasons, isBlocked);

            return new UploadValidationResult(acceptedFiles, rejectedFiles.ToArray(), batchRejectionReasons.Distinct().ToArray(), isBlocked, message);
        }

        private async Task<StagedUploadFile> StageUploadFileAsync(UploadContext context, CancellationToken cancellationToken)
        {
            string? directoryPath = Path.GetDirectoryName(context.DestinationPath);
            string temporaryPath = string.Empty;
            long totalRead = 0;

            if (string.IsNullOrEmpty(directoryPath))
                return new StagedUploadFile(string.Empty, totalRead, new InvalidDataException($"'{context.DestinationPath}' is an invalid Path."));

            temporaryPath = Path.Combine(directoryPath, $".{context.SafeFileName}.{Guid.NewGuid():N}.uploading");

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
                        return new StagedUploadFile(temporaryPath, totalRead, new InvalidDataException($"The upload exceeded the maximum size of {Options.MaxFileSizeBytes} bytes."));

                    await temporaryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    ReportProgress(context.SafeFileName, totalRead, context.File.Size);
                }

                await temporaryStream.FlushAsync(cancellationToken);

                if (context.File.Size == 0)
                    ReportProgress(context.SafeFileName, 0, 0);

                return new StagedUploadFile(temporaryPath, totalRead, null);
            }
            catch (Exception ex)
            {
                DeleteTemporaryFile(temporaryPath);
                return new StagedUploadFile(temporaryPath, totalRead, ex);
            }
        }

        public UploadContext CreateUploadContext(IBrowserFile file, string safeFileName, string directoryPath)
        {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            string destinationPath = Path.Combine(fullDirectoryPath, safeFileName);
            bool wasOverwritten = File.Exists(destinationPath);

            return new UploadContext(file, safeFileName, destinationPath, wasOverwritten);
        }

        private bool TryCommitTemporaryFile(UploadContext context, StagedUploadFile stagedUpload, out Exception? error)
        {
            if (!File.Exists(stagedUpload.TemporaryPath))
            {
                error = new FileNotFoundException("The staged upload file no longer exists.", stagedUpload.TemporaryPath);
                return false;
            }

            if (context.WasOverwritten)
            {
                if (!File.Exists(context.DestinationPath))
                {
                    error = new FileNotFoundException("The destination file no longer exists.", context.DestinationPath);
                    return false;
                }

                File.Replace(stagedUpload.TemporaryPath, context.DestinationPath, destinationBackupFileName: null);
            }
            else
            {
                if (File.Exists(context.DestinationPath))
                {
                    error = new IOException($"The destination file already exists: {context.DestinationPath}");
                    return false;
                }

                File.Move(stagedUpload.TemporaryPath, context.DestinationPath);
            }

            context.StagedUpload = stagedUpload;
            context.WasCommitted = true;
            error = null;
            return true;
        }

        private bool IsValidUploadSize(UploadContext context, StagedUploadFile stagedUpload, out Exception error)
        {
            if (stagedUpload.BytesWritten == context.File.Size)
            {
                error = null!;
                return true;
            }

            error = new InvalidDataException($"The uploaded byte count ({stagedUpload.BytesWritten}) did not match the expected byte count ({context.File.Size}).");
            return false;
        }

        private bool IsValidUploadFile(IBrowserFile file, string fileName, out UploadFileResult invalidResult)
        {
            UploadFileRejection? rejection = GetFileRejection(file);

            if (rejection != null)
            {
                invalidResult = CreateStandaloneErrorResult(file, fileName, rejection.Message!);
                return false;
            }

            invalidResult = null!;
            return true;
        }

        private bool IsValidatedUpload(UploadContext context, StagedUploadFile stagedUpload, out UploadFileResult invalidResult)
        {
            bool isValidatedUpload = true;
            invalidResult = null!;

            if (stagedUpload.Exception is OperationCanceledException)
            {
                isValidatedUpload = false;
                invalidResult = CreateCancelledResult(context);
            }

            if (stagedUpload.Exception != null)
            {
                isValidatedUpload = false;
                invalidResult = CreateErrorResult(context, stagedUpload.Exception);
            }

            if (!IsValidUploadSize(context, stagedUpload, out Exception error))
            {
                isValidatedUpload = false;
                invalidResult = CreateErrorResult(context, error);
            }

            if (!TryCommitTemporaryFile(context, stagedUpload, out Exception? commitError))
            {
                isValidatedUpload = false;
                invalidResult = CreateErrorResult(context, commitError!);
            }

            if (!isValidatedUpload)
                CleanupTemporaryFile(context);

            return isValidatedUpload;
        }

        public async Task<UploadFileResult> WriteUploadFile(IBrowserFile file, string fileName, string fullDirectoryPath, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateStandaloneCancelledResult(file, fileName);

            string normalizedFileName = Path.GetFileName(fileName);
            if (IsInvalidFileName(normalizedFileName))
                return CreateStandaloneErrorResult(file, normalizedFileName, "The upload fileName is invalid.");

            if (IsInvalidDirectoryPath(fullDirectoryPath))
                return CreateStandaloneErrorResult(file, normalizedFileName, "The upload destination directory is invalid.");

            if (!Directory.Exists(fullDirectoryPath))
                Directory.CreateDirectory(fullDirectoryPath);

            if (!IsValidUploadFile(file, normalizedFileName, out UploadFileResult invalidResult))
                return invalidResult;

            UploadContext context = CreateUploadContext(file, normalizedFileName, fullDirectoryPath);
            BeginUpload($"Uploading '{file.Name}'.");

            StagedUploadFile stagedUpload = await StageUploadFileAsync(context, cancellationToken);

            if (!IsValidatedUpload(context, stagedUpload, out UploadFileResult invalidatedResult))
                return invalidatedResult;

            CompleteUpload($"Uploaded '{file.Name}'.");
            return CreateSuccessResult(context, stagedUpload);
        }

        private UploadFileResult CreateStandaloneCancelledResult(IBrowserFile file, string safeFileName)
        {
            UploadFile uploadFile = new UploadFile(file.Name, safeFileName, string.Empty);
            UploadFileStats stats = new UploadFileStats(0, false);
            string message = $"Upload of '{file.Name}' was cancelled.";

            CancelUpload(message);

            return new UploadFileResult(uploadFile, UploadFileResultStatus.Cancelled, stats, message);
        }

        private UploadFileResult CreateStandaloneErrorResult(IBrowserFile file, string safeFileName, string message)
        {
            UploadFile uploadFile = new UploadFile(file.Name, safeFileName, string.Empty);
            UploadFileStats stats = new UploadFileStats(0, false);
            string errorMessage = $"Upload failed for '{file.Name}': {message}";

            FailUpload(errorMessage);

            return new UploadFileResult(uploadFile, UploadFileResultStatus.Error, stats, errorMessage);
        }

        private UploadFileResult CreateCancelledResult(UploadContext context)
        {
            string message = $"Upload of '{context.File.Name}' was cancelled.";

            CancelUpload(message);

            return CreateFailedUploadResult(context, UploadFileResultStatus.Cancelled, message);
        }

        private UploadFileResult CreateErrorResult(UploadContext context, Exception exception)
        {
            string message = $"Upload failed for '{context.File.Name}': {exception.Message}";

            FailUpload(message);

            return CreateFailedUploadResult(context, UploadFileResultStatus.Error, message);
        }

        private UploadFileResult CreateSuccessResult(UploadContext context, StagedUploadFile stagedUpload)
        {
            long bytesWritten = context.StagedUpload?.BytesWritten ?? 0;
            UploadFile uploadFile = new UploadFile(context.File.Name, context.SafeFileName, string.Empty);
            UploadFileStats stats = new UploadFileStats(bytesWritten, context.WasOverwritten);

            return new UploadFileResult(uploadFile, UploadFileResultStatus.Success, stats, null);
        }

        private UploadFileResult CreateFailedUploadResult(UploadContext context, UploadFileResultStatus status, string message)
        {
            long bytesWritten = context.StagedUpload?.BytesWritten ?? 0;
            UploadFile uploadFile = new UploadFile(context.File.Name, context.SafeFileName, string.Empty);
            UploadFileStats stats = new UploadFileStats(bytesWritten, context.WasOverwritten);

            return new UploadFileResult(uploadFile, status, stats, message);
        }

        private void CleanupTemporaryFile(UploadContext context)
        {
            if (!context.WasCommitted && context.StagedUpload != null)
                DeleteTemporaryFile(context.StagedUpload.TemporaryPath);
        }

        public void MarkFilesSelected(int fileCount)
        {
            StatusMessage = fileCount > 0 ? $"{fileCount} file(s) selected." : "No files were selected.";
            Status = fileCount > 0 ? UploadBoxStatus.FileSelected : UploadBoxStatus.Error;
            Progress = null;
            NotifyStateChanged();
        }

        public void BeginUpload(string message = "Uploading...")
        {
            Status = UploadBoxStatus.Uploading;
            StatusMessage = message;
            NotifyStateChanged();
        }

        public void UpdateProgress(UploadProgress progress)
        {
            StatusMessage = $"Uploading '{progress.FileName}'.";
            Status = UploadBoxStatus.Uploading;
            Progress = progress;
            NotifyStateChanged();
        }

        public void CompleteUpload(string message = "Upload complete.")
        {
            Status = UploadBoxStatus.Completed;
            StatusMessage = message;
            NotifyStateChanged();
        }

        public void FailUpload(string message)
        {
            if (string.IsNullOrEmpty(message))
                message = string.Empty;

            Status = UploadBoxStatus.Error;
            StatusMessage = message;
            NotifyStateChanged();
        }

        public void CancelUpload(string message = "Upload cancelled.")
        {
            Status = UploadBoxStatus.Cancelled;
            StatusMessage = message;
            NotifyStateChanged();
        }

        private void ReportProgress(string fileName, long bytesTransferred, long fileSizeBytes)
        {
            UploadProgress update = new(fileName, 1, 1, bytesTransferred, fileSizeBytes, bytesTransferred, fileSizeBytes);
            UpdateProgress(update);
        }

        private string CreateValidationMessage(int selectedCount, int acceptedCount, IReadOnlyList<UploadFileRejection> rejectedFiles, IReadOnlyList<UploadRejectionReason> batchRejectionReasons, bool isBlocked)
        {
            List<string> errors = new List<string>();
            bool hasBatchFileCountError = batchRejectionReasons.Contains(UploadRejectionReason.TooManyFiles);

            foreach (UploadRejectionReason reason in batchRejectionReasons.Distinct())
            {
                string? message = reason switch
                {
                    UploadRejectionReason.None => null,
                    UploadRejectionReason.EmptySelection => "No files were selected.",
                    UploadRejectionReason.TooManyFiles => $"{selectedCount} files were selected, but the maximum allowed is {Options.MaxFileCount}.",
                    _ => $"The selection failed validation: {reason}."
                };

                if (message is not null)
                    errors.Add(message);
            }

            foreach (UploadFileRejection rejection in rejectedFiles)
            {
                if (IsTooManyFiles(hasBatchFileCountError, rejection))
                    continue;

                errors.Add(rejection.Message);
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

        private static void DeleteTemporaryFile(string temporaryPath)
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
