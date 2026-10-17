using UploadBox.Models;

namespace UploadBox.Tests;

public class UploadOptionsTests
{
    [Test]
    public void Constructor_DefaultsToSingleStrictUpload()
    {
        UploadOptions options = new();

        Assert.Multiple(() =>
        {
            Assert.That(options.MaxFileCount, Is.EqualTo(1));
            Assert.That(options.MaxFileSizeBytes, Is.EqualTo(UploadOptions.DEFAULT_MAX_FILE_SIZE_BYTES));
            Assert.That(options.AllowMultipleFiles, Is.False);
            Assert.That(options.FileExtension, Is.Empty);
            Assert.That(options.Policy, Is.EqualTo(UploadPolicy.Strict));
            Assert.That(options.BufferSize, Is.EqualTo(1024 * 1024));
        });
    }

    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(100, true)]
    public void AllowMultipleFiles_ReflectsMaximumCount(int maxFileCount, bool expected)
    {
        Assert.That(new UploadOptions(maxFileCount).AllowMultipleFiles, Is.EqualTo(expected));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_RejectsInvalidMaximumFileCount(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(maxFileCount: count));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_RejectsInvalidMaximumFileSize(long size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(maxFileSizeBytes: size));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_RejectsInvalidBufferSize(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(bufferSize: size));
    }

    [Test]
    public void Constructor_RejectsUndefinedPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(policy: (UploadPolicy)42));
    }

    [TestCase(".iso", ".iso")]
    [TestCase("iso", ".iso")]
    [TestCase("  .iso  ", ".iso")]
    [TestCase("  iso  ", ".iso")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    public void Constructor_NormalizesExtension(string extension, string expected)
    {
        Assert.That(new UploadOptions(fileExtension: extension).FileExtension, Is.EqualTo(expected));
    }

    [Test]
    public void Constructor_NullExtensionMeansNoExtensionRestriction()
    {
        Assert.That(new UploadOptions(fileExtension: null).FileExtension, Is.Empty);
    }
}
