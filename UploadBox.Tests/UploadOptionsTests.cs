using UploadBox.Models;

namespace UploadBox.Tests;

/// <summary>
/// Tests upload option defaults, validation, and extension normalization.
/// </summary>
public class UploadOptionsTests
{
    /// <summary>
    /// Tests constructor defaults to single strict upload.
    /// </summary>
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

    /// <summary>
    /// Tests allow multiple files reflects maximum count.
    /// </summary>
    /// <param name="maxFileCount">Maximum number of files configured for the test.</param>
    /// <param name="expected">Expected value for the supplied input.</param>
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(100, true)]
    public void AllowMultipleFiles_ReflectsMaximumCount(int maxFileCount, bool expected)
    {
        Assert.That(new UploadOptions(maxFileCount).AllowMultipleFiles, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests constructor rejects invalid maximum file count.
    /// </summary>
    /// <param name="count">File count supplied to the tested operation.</param>
    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_RejectsInvalidMaximumFileCount(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(maxFileCount: count));
    }

    /// <summary>
    /// Tests constructor rejects invalid maximum file size.
    /// </summary>
    /// <param name="size">File size in bytes.</param>
    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_RejectsInvalidMaximumFileSize(long size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(maxFileSizeBytes: size));
    }

    /// <summary>
    /// Tests constructor rejects invalid buffer size.
    /// </summary>
    /// <param name="size">File size in bytes.</param>
    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_RejectsInvalidBufferSize(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(bufferSize: size));
    }

    /// <summary>
    /// Tests constructor rejects undefined policy.
    /// </summary>
    [Test]
    public void Constructor_RejectsUndefinedPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UploadOptions(policy: (UploadPolicy)42));
    }

    /// <summary>
    /// Tests constructor normalizes extension.
    /// </summary>
    /// <param name="extension">File extension supplied to the tested options.</param>
    /// <param name="expected">Expected value for the supplied input.</param>
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

    /// <summary>
    /// Tests constructor null extension means no extension restriction.
    /// </summary>
    [Test]
    public void Constructor_NullExtensionMeansNoExtensionRestriction()
    {
        Assert.That(new UploadOptions(fileExtension: null).FileExtension, Is.Empty);
    }
}
