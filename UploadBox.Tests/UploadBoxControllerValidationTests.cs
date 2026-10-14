using UploadBox.Models;
using UploadBox.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Tests;

public class UploadBoxControllerValidationTests
{
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

    [Test]
    public void Constructor_UsesProvidedOptions()
    {
        UploadOptions options = new(maxFileCount: 4, fileExtension: ".iso");

        Assert.That(new UploadBoxController(options).Options, Is.SameAs(options));
    }

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

    [Test]
    public void ValidateFiles_AcceptsValidFilesAndReportsReadyMessage()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2, fileExtension: ".iso"));
        IBrowserFile[] files = [new TestBrowserFile("one.ISO", [1]), new TestBrowserFile("two.iso", [2])];

        UploadValidationResult result = controller.ValidateFiles(files);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.CanUpload, Is.True);
            Assert.That(result.Files.Select(file => file.IsAccepted), Is.All.True);
            Assert.That(result.Message, Is.EqualTo("2 file(s) are ready to upload."));
        });
    }

    [Test]
    public void ValidateFiles_RejectsWrongExtensionCaseInsensitively()
    {
        UploadBoxController controller = new(new UploadOptions(fileExtension: "iso"));

        UploadValidationResult result = controller.ValidateFiles([new TestBrowserFile("disk.bin", [1])]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.Files.Single().Reason, Is.EqualTo(UploadRejectionReason.InvalidExtension));
            Assert.That(result.Files.Single().Message, Does.Contain(".iso"));
            Assert.That(result.Message, Does.Contain("The upload request was rejected."));
        });
    }

    [Test]
    public void ValidateFiles_ReportsEachInvalidExtensionWithPluralSummary()
    {
        UploadBoxController controller = new(new UploadOptions(fileExtension: ".iso", policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([
            new TestBrowserFile("one.bin", [1]), new TestBrowserFile("two.bin", [2])]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.Message, Does.Contain("2 validation errors found."));
            Assert.That(result.Message, Does.Contain("1. 'one.bin'"));
            Assert.That(result.Message, Does.Contain("2. 'two.bin'"));
        });
    }

    [Test]
    public void ValidateFiles_RejectsFileLargerThanLimitButAcceptsExactLimit()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileSizeBytes: 2));

        UploadValidationResult result = controller.ValidateFiles([
            new TestBrowserFile("exact.iso", [1, 2]),
            new TestBrowserFile("large.iso", [1, 2, 3])]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files[0].IsAccepted, Is.True);
            Assert.That(result.Files[1].Reason, Is.EqualTo(UploadRejectionReason.FileTooLarge));
        });
    }

    [Test]
    public void ValidateFiles_StrictPolicyBlocksMixedValidAndInvalidFiles()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2, maxFileSizeBytes: 1));

        UploadValidationResult result = controller.ValidateFiles([
            new TestBrowserFile("valid.iso", [1]),
            new TestBrowserFile("large.iso", [1, 2], "application/octet-stream")]);

        Assert.That(result.IsBlocked, Is.True);
    }

    [Test]
    public void ValidateFiles_SkipInvalidFilesAllowsAcceptedFilesAndReportsRejectedOnes()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2, maxFileSizeBytes: 1, policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([
            new TestBrowserFile("valid.iso", [1]),
            new TestBrowserFile("large.iso", [1, 2])]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.CanUpload, Is.True);
            Assert.That(result.Message, Does.Contain("1 of 2 file(s) are ready to upload."));
            Assert.That(result.Message, Does.Contain("Rejected files will be skipped."));
        });
    }

    [Test]
    public void ValidateFiles_SkipInvalidFilesBlocksWhenNoFileIsAccepted()
    {
        UploadBoxController controller = new(new UploadOptions(fileExtension: ".iso", policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([new TestBrowserFile("bad.bin", [1])]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.CanUpload, Is.False);
        });
    }

    [Test]
    public void ValidateFiles_EnforcesMaximumCountAndSummarizesExcessOnce()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 1, policy: UploadPolicy.SkipInvalidFiles));

        UploadValidationResult result = controller.ValidateFiles([
            new TestBrowserFile("one.iso", [1]), new TestBrowserFile("two.iso", [2]), new TestBrowserFile("three.iso", [3])]);

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
