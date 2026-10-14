using UploadBox.Models;

namespace UploadBox.Tests;

public class UploadProgressTests
{
    [Test]
    public void UploadBatchProgressContext_StoresBatchPositionAndTotals()
    {
        UploadBatchProgressContext context = new(2, 4, 1024, 4096);

        Assert.Multiple(() =>
        {
            Assert.That(context.FileIndex, Is.EqualTo(2));
            Assert.That(context.FileCount, Is.EqualTo(4));
            Assert.That(context.CompletedBytesBeforeCurrentFile, Is.EqualTo(1024));
            Assert.That(context.TotalSizeBytes, Is.EqualTo(4096));
        });
    }

    [TestCase(0, 1, 0, 1, "fileIndex")]
    [TestCase(2, 1, 0, 1, "fileIndex")]
    [TestCase(1, 0, 0, 1, "fileCount")]
    [TestCase(1, 1, -1, 1, "completedBytesBeforeCurrentFile")]
    [TestCase(1, 1, 0, -1, "totalSizeBytes")]
    [TestCase(1, 1, 2, 1, "totalSizeBytes")]
    public void UploadBatchProgressContext_RejectsInvalidValues(int index, int count, long completedBytes, long totalBytes, string parameterName)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new UploadBatchProgressContext(index, count, completedBytes, totalBytes))!;

        Assert.That(exception.ParamName, Is.EqualTo(parameterName));
    }

    [TestCase(-1)]
    public void UploadBatchProgressContext_SingleFile_RejectsNegativeSize(long fileSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UploadBatchProgressContext.SingleFile(fileSize));
    }

    [TestCase(0, 100, 0)]
    [TestCase(25, 100, 25)]
    [TestCase(1, 3, 33.33)]
    [TestCase(100, 100, 100)]
    [TestCase(150, 100, 100)]
    [TestCase(-10, 100, 0)]
    [TestCase(0, 0, 100)]
    public void GetFilePercentage_CalculatesAndClamps(long transferred, long size, double expected)
    {
        UploadProgress progress = new("image.iso", 1, 1, transferred, size, transferred, size);

        Assert.That(progress.GetFilePercentage(), Is.EqualTo(expected));
    }

    [TestCase(1, 2, 50)]
    [TestCase(2, 3, 66.67)]
    [TestCase(5, 4, 100)]
    public void GetOverallPercentage_UsesAggregateByteCounts(long transferred, long size, double expected)
    {
        UploadProgress progress = new("image.iso", 1, 3, 1, 1, transferred, size);

        Assert.That(progress.GetOverallPercentage(), Is.EqualTo(expected));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void GetOverallPercentage_ReturnsZeroWhenFileCountIsNotPositive(int fileCount)
    {
        UploadProgress progress = new("image.iso", 1, fileCount, 1, 2, 1, 2);

        Assert.That(progress.GetOverallPercentage(), Is.Zero);
    }

    [TestCase(1, 3, 1, 33.3)]
    [TestCase(1, 8, 2, 12.5)]
    [TestCase(1, 32, 2, 3.13)]
    public void GetFilePercentage_AppliesRequestedPrecision(long transferred, long size, int precision, double expected)
    {
        UploadProgress progress = new("image.iso", 1, 1, transferred, size, transferred, size);

        Assert.That(progress.GetFilePercentage(precision), Is.EqualTo(expected));
    }

    [TestCase(-4, 33)]
    [TestCase(16, 33.333333333333336)]
    public void GetFilePercentage_ClampsPrecisionToSupportedRange(int precision, double expected)
    {
        UploadProgress progress = new("image.iso", 1, 1, 1, 3, 1, 3);

        Assert.That(progress.GetFilePercentage(precision), Is.EqualTo(expected));
    }
}
