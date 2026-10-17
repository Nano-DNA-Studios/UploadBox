using UploadBox.Models;
using UploadBox.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Tests;

/// <summary>
/// Tests upload controller initialization, selection validation, and status updates.
/// </summary>
public class UploadBoxControllerValidationTests
{
    /// <summary>
    /// Tests constructor defaults to idle with default options.
    /// </summary>
    [Test]
    public void Constructor_DefaultsToIdleWithDefaultOptions()
    {
        UploadBoxController controller = new();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status, Is.EqualTo(UploadBoxStatus.Idle));
            Assert.That(controller.StatusMessage, Is.Empty);
            Assert.That(controller.Progress, Is.Null);
            Assert.That(controller.IsUploading, Is.False);
            Assert.That(controller.Options.MaxFileCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Tests constructor uses provided options.
    /// </summary>
    [Test]
    public void Constructor_UsesProvidedOptions()
    {
        UploadOptions options = new(maxFileCount: 4, fileExtension: ".iso");

        Assert.That(new UploadBoxController(options).Options, Is.SameAs(options));
    }

    /// <summary>
    /// Tests validate files null or empty selection is blocked.
    /// </summary>
    [Test]
    public void ValidateFiles_NullOrEmptySelectionIsBlocked()
    {
        UploadBoxController controller = new();

        UploadValidationResult nullResult = controller.ValidateFiles(null!);
        UploadValidationResult emptyResult = controller.ValidateFiles(Array.Empty<IBrowserFile>());

        Assert.Multiple(() =>
        {
            Assert.That(nullResult.IsBlocked, Is.True);
            Assert.That(nullResult.CanUpload, Is.False);
            Assert.That(nullResult.Files, Is.Empty);
            Assert.That(nullResult.Message, Is.EqualTo("No files were selected."));
            Assert.That(emptyResult.IsBlocked, Is.True);
            Assert.That(emptyResult.Message, Is.EqualTo(nullResult.Message));
        });
    }

    /// <summary>
    /// Tests validate files accepts valid files and reports ready message.
    /// </summary>
    [Test]
    public void ValidateFiles_AcceptsValidFilesAndReportsReadyMessage()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2, fileExtension: ".iso"));
        IBrowserFile[] files = [TestUpload.UppercaseIso, TestUpload.TwoByteIso];

        UploadValidationResult result = controller.ValidateFiles(files);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.CanUpload, Is.True);
            Assert.That(result.Files.Select(file => file.IsAccepted), Is.All.True);
            Assert.That(result.Message, Is.EqualTo("2 file(s) are ready to upload."));
        });
    }

    /// <summary>
    /// Tests validate files rejects wrong extension case insensitively.
    /// </summary>
    [Test]
    public void ValidateFiles_RejectsWrongExtensionCaseInsensitively()
    {
        UploadBoxController controller = new(new UploadOptions(fileExtension: "iso"));

        UploadValidationResult result = controller.ValidateFiles([TestUpload.WrongExtension]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.Files.Single().Reason, Is.EqualTo(UploadRejectionReason.InvalidExtension));
            Assert.That(result.Files.Single().Message, Does.Contain(".iso"));
            Assert.That(result.Message, Does.Contain("The upload request was rejected."));
        });
    }

    /// <summary>
    /// Tests validate files reports each invalid extension with plural summary.
    /// </summary>
    [Test]
    public void ValidateFiles_ReportsEachInvalidExtensionWithPluralSummary()
    {
        UploadBoxController controller = new(new UploadOptions(fileExtension: ".iso", policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([
            TestUpload.FirstWrongExtension, TestUpload.SecondWrongExtension]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.Message, Does.Contain("2 validation errors found."));
            Assert.That(result.Message, Does.Contain("1. 'one.bin'"));
            Assert.That(result.Message, Does.Contain("2. 'two.bin'"));
        });
    }

    /// <summary>
    /// Tests validate files rejects file larger than limit but accepts exact limit.
    /// </summary>
    [Test]
    public void ValidateFiles_RejectsFileLargerThanLimitButAcceptsExactLimit()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileSizeBytes: 2));

        UploadValidationResult result = controller.ValidateFiles([
            TestUpload.ExactLimitIso,
            TestUpload.OversizedIso]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files[0].IsAccepted, Is.True);
            Assert.That(result.Files[1].Reason, Is.EqualTo(UploadRejectionReason.FileTooLarge));
        });
    }

    /// <summary>
    /// Tests validate files strict policy blocks mixed valid and invalid files.
    /// </summary>
    [Test]
    public void ValidateFiles_StrictPolicyBlocksMixedValidAndInvalidFiles()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2, maxFileSizeBytes: 1));

        UploadValidationResult result = controller.ValidateFiles([
            TestUpload.OneByteIso,
            TestUpload.ExactLimitIso]);

        Assert.That(result.IsBlocked, Is.True);
    }

    /// <summary>
    /// Tests validate files skip invalid files allows accepted files and reports rejected ones.
    /// </summary>
    [Test]
    public void ValidateFiles_SkipInvalidFilesAllowsAcceptedFilesAndReportsRejectedOnes()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2, maxFileSizeBytes: 1, policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([
            TestUpload.OneByteIso,
            TestUpload.ExactLimitIso]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.CanUpload, Is.True);
            Assert.That(result.Message, Does.Contain("1 of 2 file(s) are ready to upload."));
            Assert.That(result.Message, Does.Contain("Rejected files will be skipped."));
        });
    }

    /// <summary>
    /// Tests validate files skip invalid files blocks when no file is accepted.
    /// </summary>
    [Test]
    public void ValidateFiles_SkipInvalidFilesBlocksWhenNoFileIsAccepted()
    {
        UploadBoxController controller = new(new UploadOptions(fileExtension: ".iso", policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([TestUpload.WrongExtension]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.CanUpload, Is.False);
        });
    }

    /// <summary>
    /// Tests validate files enforces maximum count and summarizes excess once.
    /// </summary>
    [Test]
    public void ValidateFiles_EnforcesMaximumCountAndSummarizesExcessOnce()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 1, policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([
            TestUpload.OneByteIso, TestUpload.TwoByteIso, TestUpload.ThreeByteIso]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files.Select(file => file.Reason), Is.EqualTo(new[]
            {
                UploadRejectionReason.None, UploadRejectionReason.TooManyFiles, UploadRejectionReason.TooManyFiles
            }));
            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.Message, Does.Contain("3 files were selected, but the maximum allowed is 1."));
            Assert.That(result.Message.Split("validation error").Length - 1, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Tests mark files selected updates state and notifies.
    /// </summary>
    /// <param name="count">File count supplied to the tested operation.</param>
    /// <param name="status">Expected controller status.</param>
    /// <param name="message">Expected status message.</param>
    [TestCase(0, UploadBoxStatus.Idle, "")]
    [TestCase(-2, UploadBoxStatus.Idle, "")]
    [TestCase(1, UploadBoxStatus.FileSelected, "1 file(s) selected.")]
    [TestCase(3, UploadBoxStatus.FileSelected, "3 file(s) selected.")]
    public void MarkFilesSelected_UpdatesStateAndNotifies(int count, UploadBoxStatus status, string message)
    {
        UploadBoxController controller = new();
        int notificationCount = 0;
        controller.StateChanged += (_, _) => notificationCount++;

        controller.MarkFilesSelected(count);

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status, Is.EqualTo(status));
            Assert.That(controller.StatusMessage, Is.EqualTo(message));
            Assert.That(controller.Progress, Is.Null);
            Assert.That(notificationCount, Is.EqualTo(1));
        });
    }
}
