using UploadBox.Models;

namespace UploadBox.Tests;

public class UploadProgressTests
{
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
