
namespace UploadBox.Models
{
    /// <summary>
    /// Reports per-file and aggregate byte counts for an upload batch.
    /// </summary>
    /// <param name="FileName">Name reported for the file being transferred.</param>
    /// <param name="FileIndex">One-based position of the file in the batch.</param>
    /// <param name="FileCount">Number of files in the batch.</param>
    /// <param name="BytesTransferred">Bytes transferred for the current file.</param>
    /// <param name="FileSizeBytes">Browser-reported size of the current file, in bytes.</param>
    /// <param name="TotalBytesTransferred">Bytes transferred for the batch, including preceding files.</param>
    /// <param name="TotalSizeBytes">Combined size of the batch, in bytes.</param>
    public sealed record UploadProgress(string FileName, int FileIndex, int FileCount, long BytesTransferred, long FileSizeBytes, long TotalBytesTransferred, long TotalSizeBytes)
    {
        /// <summary>
        /// Gets the transferred percentage for the current file.
        /// </summary>
        /// <param name="precision">Number of decimal places in the returned value.</param>
        /// <returns>The current file's percentage, rounded to the requested precision.</returns>
        public double GetFilePercentage(int precision = 2) => UploadProgressMath.CalculatePercentage(BytesTransferred, FileSizeBytes, precision);

        /// <summary>
        /// Gets the transferred percentage for the entire batch.
        /// </summary>
        /// <param name="precision">Number of decimal places in the returned value.</param>
        /// <returns>Zero when the batch has no files; otherwise, the aggregate percentage rounded to the requested precision.</returns>
        public double GetOverallPercentage(int precision = 2)
        {
            if (FileCount < 1)
                return 0;

            return UploadProgressMath.CalculatePercentage(TotalBytesTransferred, TotalSizeBytes, precision);
        }
    }

    /// <summary>
    /// Provides percentage calculations shared by upload progress values.
    /// </summary>
    internal static class UploadProgressMath
    {
        /// <summary>
        /// Calculates a transfer percentage and rounds it to the supported precision.
        /// </summary>
        /// <param name="transferredBytes">Number of bytes transferred.</param>
        /// <param name="totalBytes">Total number of bytes.</param>
        /// <param name="precision">Requested number of decimal places; values are limited to the range zero through fifteen.</param>
        /// <returns>One hundred when the total is zero; otherwise, the calculated percentage rounded away from zero and clamped to the range zero through one hundred.</returns>
        public static double CalculatePercentage(long transferredBytes, long totalBytes, int precision)
        {
            if (precision < 0 || precision > 15)
                precision = int.Max(int.Min(15, precision), 0);

            if (totalBytes == 0)
                return 100d;

            if (totalBytes < 0)
                totalBytes = 0;

            double percentage = transferredBytes * 100d / totalBytes;
            percentage = Math.Clamp(percentage, 0d, 100d);

            return Math.Round(percentage, precision, MidpointRounding.AwayFromZero);
        }
    }
}
