using UploadBox.Models;

namespace UploadBox.Tests;

/// <summary>
/// Tests upload batch context validation and file and aggregate progress calculations.
/// </summary>
public class UploadProgressTests
{
    /// <summary>
    /// Tests upload batch progress context stores batch position and totals.
    /// </summary>
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

    /// <summary>
    /// Tests upload batch progress context rejects invalid values.
    /// </summary>
    /// <param name="index">Current file index supplied to the batch context.</param>
    /// <param name="count">File count supplied to the tested operation.</param>
    /// <param name="completedBytes">Bytes completed before the current file.</param>
    /// <param name="totalBytes">Total batch size in bytes.</param>
    /// <param name="parameterName">Expected parameter name reported by the exception.</param>
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

    /// <summary>
    /// Tests upload batch progress context single file rejects negative size.
    /// </summary>
    /// <param name="fileSize">File size supplied to the tested operation.</param>
    [TestCase(-1)]
    public void UploadBatchProgressContext_SingleFile_RejectsNegativeSize(long fileSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UploadBatchProgressContext.SingleFile(fileSize));
    }

    /// <summary>
    /// Tests get file percentage calculates and clamps.
    /// </summary>
    /// <param name="transferred">Number of bytes transferred.</param>
    /// <param name="size">File size in bytes.</param>
    /// <param name="expected">Expected value for the supplied input.</param>
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

    /// <summary>
    /// Tests get overall percentage uses aggregate byte counts.
    /// </summary>
    /// <param name="transferred">Number of bytes transferred.</param>
    /// <param name="size">File size in bytes.</param>
    /// <param name="expected">Expected value for the supplied input.</param>
    [TestCase(1, 2, 50)]
    [TestCase(2, 3, 66.67)]
    [TestCase(5, 4, 100)]
    public void GetOverallPercentage_UsesAggregateByteCounts(long transferred, long size, double expected)
    {
        UploadProgress progress = new("image.iso", 1, 3, 1, 1, transferred, size);

        Assert.That(progress.GetOverallPercentage(), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests get overall percentage returns zero when file count is not positive.
    /// </summary>
    /// <param name="fileCount">Number of files in the batch.</param>
    [TestCase(0)]
    [TestCase(-1)]
    public void GetOverallPercentage_ReturnsZeroWhenFileCountIsNotPositive(int fileCount)
    {
        UploadProgress progress = new("image.iso", 1, fileCount, 1, 2, 1, 2);

        Assert.That(progress.GetOverallPercentage(), Is.Zero);
    }

    /// <summary>
    /// Tests get file percentage applies requested precision.
    /// </summary>
    /// <param name="transferred">Number of bytes transferred.</param>
    /// <param name="size">File size in bytes.</param>
    /// <param name="precision">Decimal precision requested for the percentage.</param>
    /// <param name="expected">Expected value for the supplied input.</param>
    [TestCase(1, 3, 1, 33.3)]
    [TestCase(1, 8, 2, 12.5)]
    [TestCase(1, 32, 2, 3.13)]
    public void GetFilePercentage_AppliesRequestedPrecision(long transferred, long size, int precision, double expected)
    {
        UploadProgress progress = new("image.iso", 1, 1, transferred, size, transferred, size);

        Assert.That(progress.GetFilePercentage(precision), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests get file percentage clamps precision to supported range.
    /// </summary>
    /// <param name="precision">Decimal precision requested for the percentage.</param>
    /// <param name="expected">Expected value for the supplied input.</param>
    [TestCase(-4, 33)]
    [TestCase(16, 33.333333333333336)]
    public void GetFilePercentage_ClampsPrecisionToSupportedRange(int precision, double expected)
    {
        UploadProgress progress = new("image.iso", 1, 1, 1, 3, 1, 3);

        Assert.That(progress.GetFilePercentage(precision), Is.EqualTo(expected));
    }
}
