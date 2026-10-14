using UploadBox.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Tests;

public class UploadModelTests
{
    [Test]
    public void UploadRequest_ExposesFilesAndCount()
    {
        IBrowserFile[] files = [TestUpload.OneByteIso, TestUpload.TwoByteIso];
        UploadRequest request = new(files);

        Assert.That(request.Files, Is.SameAs((IReadOnlyList<IBrowserFile>)files));
        Assert.That(request.FileCount, Is.EqualTo(2));
    }

    [TestCase(UploadRejectionReason.None, true)]
    [TestCase(UploadRejectionReason.EmptySelection, false)]
    [TestCase(UploadRejectionReason.TooManyFiles, false)]
    [TestCase(UploadRejectionReason.FileTooLarge, false)]
    [TestCase(UploadRejectionReason.InvalidExtension, false)]
    public void UploadFileValidationResult_MapsReasonToAcceptance(UploadRejectionReason reason, bool accepted)
    {
        IBrowserFile file = TestUpload.ValidIso;
        UploadFileValidationResult result = new(file, reason, "message");

        Assert.Multiple(() =>
        {
            Assert.That(result.File, Is.SameAs(file));
            Assert.That(result.FileName, Is.EqualTo("disk.iso"));
            Assert.That(result.FileSize, Is.EqualTo(3));
            Assert.That(result.Reason, Is.EqualTo(reason));
            Assert.That(result.Message, Is.EqualTo("message"));
            Assert.That(result.IsAccepted, Is.EqualTo(accepted));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    [TestCase(true, true, false)]
    public void UploadValidationResult_CanUploadRequiresUnblockedAcceptedFile(bool blocked, bool accepted, bool expected)
    {
        UploadFileValidationResult[] results = accepted
            ? [new(TestUpload.OneByteIso, UploadRejectionReason.None, string.Empty)]
            : [];
        UploadValidationResult result = new(results, blocked, "summary");

        Assert.That(result.CanUpload, Is.EqualTo(expected));
    }

    [Test]
    public void UploadContext_NewContextUsesFullDirectoryAndDetectsExistingFile()
    {
        using TemporaryDirectory directory = new();
        string destination = Path.Combine(directory.Path, "disk.iso");
        File.WriteAllText(destination, "existing");
        IBrowserFile file = TestUpload.SourceIso;

        UploadContext context = UploadContext.NewContext(file, "disk.iso", directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(context.File, Is.SameAs(file));
            Assert.That(context.SafeFileName, Is.EqualTo("disk.iso"));
            Assert.That(context.DestinationPath, Is.EqualTo(destination));
            Assert.That(context.WasOverwritten, Is.True);
        });
    }

    [Test]
    public void UploadContext_NewContextReportsNewDestination()
    {
        using TemporaryDirectory directory = new();

        UploadContext context = UploadContext.NewContext(TestUpload.EmptyIso, "disk.iso", directory.Path);

        Assert.That(context.WasOverwritten, Is.False);
    }

    [Test]
    public void UploadResultRecords_RetainValues()
    {
        UploadFile file = new("original.iso", "saved.iso", "C:\\uploads\\saved.iso");
        UploadFileStats stats = new(123, true);

        UploadSucceeded success = new(file, stats);
        Assert.Multiple(() =>
        {
            Assert.That(success.File, Is.EqualTo(file));
            Assert.That(success.Stats.BytesWritten, Is.EqualTo(123));
            Assert.That(success.Stats.WasOverwritten, Is.True);
        });
        Assert.That(new UploadCancelled("original.iso").FileName, Is.EqualTo("original.iso"));
        Assert.That(new UploadFailed("original.iso", UploadFailureCode.TransferFailed, "failed").UserMessage, Is.EqualTo("failed"));
        Assert.That(new StagedUploadFile("temp", 123, null).BytesWritten, Is.EqualTo(123));
    }
}
