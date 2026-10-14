using UploadBox.Models;
using UploadBox.Services;

namespace UploadBox.Tests;

public class UploadBoxControllerWriteTests
{
    [Test]
    public async Task WriteUploadFile_WhenCalledSequentially_WritesEveryFileAndReportsProgressForEach()
    {
        using TemporaryDirectory directory = new();
        UploadBoxController controller = new(new UploadOptions(bufferSize: 1));
        List<string> progressFileNames = [];
        controller.StateChanged += (_, _) =>
        {
            if (controller.Progress is UploadProgress progress)
                progressFileNames.Add(progress.FileName);
        };

        controller.MarkFilesSelected(2);
        UploadFileResult firstResult = await controller.WriteUploadFile(TestUpload.OneByteIso, "one.iso", directory.Path, CancellationToken.None);
        controller.MarkFilesSelected(2);
        UploadFileResult secondResult = await controller.WriteUploadFile(TestUpload.TwoByteIso, "two.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstResult, Is.TypeOf<UploadSucceeded>());
            Assert.That(secondResult, Is.TypeOf<UploadSucceeded>());
            Assert.That(File.ReadAllBytes(Path.Combine(directory.Path, "one.iso")), Is.EqualTo(new byte[] { 1 }));
            Assert.That(File.ReadAllBytes(Path.Combine(directory.Path, "two.iso")), Is.EqualTo(new byte[] { 2 }));
            Assert.That(progressFileNames, Does.Contain("one.iso"));
            Assert.That(progressFileNames, Does.Contain("two.iso"));
        });
    }

    [Test]
    public async Task WriteUploadFile_WithBatchContext_ReportsFileAndAggregateProgress()
    {
        using TemporaryDirectory directory = new();
        UploadBoxController controller = new(new UploadOptions(bufferSize: 1));
        List<UploadProgress> progressUpdates = [];
        controller.StateChanged += (_, _) =>
        {
            if (controller.Progress is UploadProgress progress)
                progressUpdates.Add(progress);
        };
        UploadBatchProgressContext context = new(2, 3, 4, 10);

        UploadFileResult result = await controller.WriteUploadFile(
            TestUpload.ExactLimitIso,
            "two.iso",
            directory.Path,
            context,
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<UploadSucceeded>());
        Assert.That(progressUpdates, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(progressUpdates.All(progress => progress.FileIndex == 2 && progress.FileCount == 3), Is.True);
            Assert.That(progressUpdates.Take(2).Select(progress => progress.BytesTransferred), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(progressUpdates.Take(2).Select(progress => progress.TotalBytesTransferred), Is.EqualTo(new long[] { 5, 6 }));
            Assert.That(progressUpdates.All(progress => progress.TotalSizeBytes == 10 && progress.FileSizeBytes == 2), Is.True);
            Assert.That(progressUpdates[^1].GetFilePercentage(), Is.EqualTo(100));
            Assert.That(progressUpdates[^1].GetOverallPercentage(), Is.EqualTo(60));
        });
    }

    [Test]
    public void WriteUploadFile_WithBatchContext_RejectsTotalSmallerThanCurrentFileAndOffset()
    {
        using TemporaryDirectory directory = new();
        UploadBoxController controller = new();
        UploadBatchProgressContext context = new(2, 2, 3, 4);

        Assert.ThrowsAsync<ArgumentException>(async () => await controller.WriteUploadFile(
            TestUpload.SourceIso,
            "two.iso",
            directory.Path,
            context,
            CancellationToken.None));
    }

    [Test]
    public async Task WriteUploadFile_WritesNewFileAndReportsProgressAndCompletion()
    {
        using TemporaryDirectory directory = new();
        UploadBoxController controller = new(new UploadOptions(bufferSize: 2));
        List<UploadBoxStatus> states = [];
        controller.StateChanged += (_, _) => states.Add(controller.Status);

        UploadFileResult result = await controller.WriteUploadFile(TestUpload.SourceIso, "saved.iso", directory.Path, CancellationToken.None);

        UploadSucceeded success = result as UploadSucceeded ?? throw new AssertionException("Expected a successful upload.");
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(Path.Combine(directory.Path, "saved.iso")), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(success.File.OriginiOriginalFileName, Is.EqualTo("source.iso"));
            Assert.That(success.File.SavedFileName, Is.EqualTo("saved.iso"));
            Assert.That(success.File.SavedPath, Is.EqualTo(Path.Combine(directory.Path, "saved.iso")));
            Assert.That(success.Stats.BytesWritten, Is.EqualTo(3));
            Assert.That(success.Stats.WasOverwritten, Is.False);
            Assert.That(controller.Status, Is.EqualTo(UploadBoxStatus.Completed));
            Assert.That(controller.StatusMessage, Is.EqualTo("Uploaded 'source.iso'."));
            Assert.That(controller.IsUploading, Is.False);
            Assert.That(controller.Progress?.BytesTransferred, Is.EqualTo(3));
            Assert.That(states, Does.Contain(UploadBoxStatus.Uploading));
            Assert.That(states[^1], Is.EqualTo(UploadBoxStatus.Completed));
            Assert.That(Directory.GetFiles(directory.Path, "*.uploading"), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_CreatesMissingDestinationDirectory()
    {
        string parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(parent, "nested");
        try
        {
            UploadFileResult result = await new UploadBoxController().WriteUploadFile(TestUpload.NineByteIso, "a.iso", destination, CancellationToken.None);

            Assert.That(result, Is.TypeOf<UploadSucceeded>());
            Assert.That(File.ReadAllBytes(Path.Combine(destination, "a.iso")), Is.EqualTo(new byte[] { 9 }));
        }
        finally
        {
            if (Directory.Exists(parent))
                Directory.Delete(parent, recursive: true);
        }
    }

    [Test]
    public async Task WriteUploadFile_OverwritesAnExistingFile()
    {
        using TemporaryDirectory directory = new();
        string destination = Path.Combine(directory.Path, "saved.iso");
        File.WriteAllBytes(destination, [8, 8]);

        UploadFileResult result = await new UploadBoxController().WriteUploadFile(TestUpload.SourceIso, "saved.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadSucceeded>());
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(((UploadSucceeded)result).Stats.WasOverwritten, Is.True);
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase(" ")]
    [TestCase(".")]
    [TestCase("..")]
    [TestCase("folder/..")] // Becomes ".." after GetFileName on supported path syntax.
    public async Task WriteUploadFile_RejectsInvalidFileNames(string? fileName)
    {
        using TemporaryDirectory directory = new();

        UploadFileResult result = await new UploadBoxController().WriteUploadFile(TestUpload.SourceOneByteIso, fileName!, directory.Path, CancellationToken.None);

        Assert.That(result, Is.EqualTo(new UploadFailed("source.iso", UploadFailureCode.InvalidFileName, "The file name is invalid.")));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("..")] 
    public async Task WriteUploadFile_RejectsInvalidDirectory(string path)
    {
        UploadBoxController controller = new();

        UploadFileResult result = await controller.WriteUploadFile(TestUpload.SourceOneByteIso, "saved.iso", path, CancellationToken.None);

        Assert.That(result, Is.TypeOf<UploadFailed>());
        Assert.That(((UploadFailed)result).Code, Is.EqualTo(UploadFailureCode.DestinationUnavailable));
    }

    [Test]
    public async Task WriteUploadFile_PreCancelledTokenDoesNotCreateFile()
    {
        using TemporaryDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        UploadFileResult result = await new UploadBoxController().WriteUploadFile(TestUpload.SourceOneByteIso, "saved.iso", directory.Path, cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadCancelled>());
            Assert.That(((UploadCancelled)result).FileName, Is.EqualTo("source.iso"));
            Assert.That(Directory.GetFiles(directory.Path), Is.Empty);
        });
    }

    [TestCase("source.bin", UploadFailureCode.InvalidExtension)]
    [TestCase("large.iso", UploadFailureCode.FileTooLarge)]
    public async Task WriteUploadFile_RejectsInvalidFileBeforeStaging(string fileName, UploadFailureCode code)
    {
        using TemporaryDirectory directory = new();
        UploadBoxController controller = new(new UploadOptions(maxFileSizeBytes: 1, fileExtension: ".iso"));
        TestBrowserFile file = fileName == "large.iso" ? TestUpload.OversizedIso : TestUpload.WrongExtension;

        UploadFileResult result = await controller.WriteUploadFile(file, "saved.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadFailed>());
            Assert.That(((UploadFailed)result).Code, Is.EqualTo(code));
            Assert.That(controller.Status, Is.EqualTo(UploadBoxStatus.Error));
            Assert.That(Directory.GetFiles(directory.Path), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_RejectsMismatchBetweenDeclaredAndReadBytesAndCleansTemporaryFile()
    {
        using TemporaryDirectory directory = new();
        TestBrowserFile file = new("short.iso", 4, () => new MemoryStream([1, 2, 3]));
        UploadBoxController controller = new();

        UploadFileResult result = await controller.WriteUploadFile(file, "saved.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadFailed>());
            Assert.That(((UploadFailed)result).Code, Is.EqualTo(UploadFailureCode.SizeMismatch));
            Assert.That(controller.Status, Is.EqualTo(UploadBoxStatus.Error));
            Assert.That(Directory.GetFiles(directory.Path), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_RejectsActualTransferAboveConfiguredLimit()
    {
        using TemporaryDirectory directory = new();
        TestBrowserFile file = new("oversized-during-transfer.iso", 1, () => new MemoryStream([1, 2]));
        UploadBoxController controller = new(new UploadOptions(maxFileSizeBytes: 1));

        UploadFileResult result = await controller.WriteUploadFile(file, "saved.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadFailed>());
            Assert.That(((UploadFailed)result).Code, Is.EqualTo(UploadFailureCode.FileTooLarge));
            Assert.That(Directory.GetFiles(directory.Path), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_SanitizesDestinationFileName()
    {
        using TemporaryDirectory directory = new();
        string input = $"parent{Path.DirectorySeparatorChar}saved.iso";

        UploadFileResult result = await new UploadBoxController().WriteUploadFile(TestUpload.SourceIso, input, directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadSucceeded>());
            Assert.That(File.Exists(Path.Combine(directory.Path, "saved.iso")), Is.True);
            Assert.That(Directory.GetDirectories(directory.Path), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_MapsStreamFailureAndCleansTemporaryFile()
    {
        using TemporaryDirectory directory = new();
        TestBrowserFile file = new("broken.iso", 4, () => throw new IOException("read failed"));
        UploadBoxController controller = new();

        UploadFileResult result = await controller.WriteUploadFile(file, "saved.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadFailed>());
            Assert.That(((UploadFailed)result).Code, Is.EqualTo(UploadFailureCode.TransferFailed));
            Assert.That(controller.Status, Is.EqualTo(UploadBoxStatus.Error));
            Assert.That(Directory.GetFiles(directory.Path), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_CancellationDuringTransferReturnsCancelledAndCleansTemporaryFile()
    {
        using TemporaryDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        CancellationGateStream stream = new();
        TestBrowserFile file = new("cancel.iso", 1, () => stream);
        UploadBoxController controller = new();

        Task<UploadFileResult> upload = controller.WriteUploadFile(file, "saved.iso", directory.Path, cancellation.Token);
        await stream.Started.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        UploadFileResult result = await upload;

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadCancelled>());
            Assert.That(controller.Status, Is.EqualTo(UploadBoxStatus.Cancelled));
            Assert.That(controller.StatusMessage, Is.EqualTo("Upload of 'cancel.iso' was cancelled."));
            Assert.That(Directory.GetFiles(directory.Path), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_RejectsDestinationCreatedDuringStagingAsConflict()
    {
        using TemporaryDirectory directory = new();
        string destination = Path.Combine(directory.Path, "saved.iso");
        TestBrowserFile file = new("source.iso", 1, () =>
        {
            File.WriteAllText(destination, "concurrent file");
            return new MemoryStream([1]);
        });
        UploadBoxController controller = new();

        UploadFileResult result = await controller.WriteUploadFile(file, "saved.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadFailed>());
            Assert.That(((UploadFailed)result).Code, Is.EqualTo(UploadFailureCode.DestinationConflict));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("concurrent file"));
            Assert.That(Directory.GetFiles(directory.Path, "*.uploading"), Is.Empty);
        });
    }

    [Test]
    public async Task WriteUploadFile_AllowsZeroLengthFileAndReportsHundredPercent()
    {
        using TemporaryDirectory directory = new();
        UploadBoxController controller = new();

        UploadFileResult result = await controller.WriteUploadFile(TestUpload.EmptyIso, "empty.iso", directory.Path, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<UploadSucceeded>());
            Assert.That(File.ReadAllBytes(Path.Combine(directory.Path, "empty.iso")), Is.Empty);
            Assert.That(controller.Progress?.GetFilePercentage(), Is.EqualTo(100));
        });
    }
}
