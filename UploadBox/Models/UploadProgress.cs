
namespace UploadBox.Models
{
    public sealed record UploadProgress(string FileName, int FileIndex, int FileCount, long BytesTransferred, long FileSizeBytes, long TotalBytesTransferred, long TotalSizeBytes)
    {
        public double GetFilePercentage(int precision = 2) => UploadProgressMath.CalculatePercentage(BytesTransferred, FileSizeBytes, precision);

        public double GetOverallPercentage(int precision = 2)
        {
            if (FileCount < 1)
                return 0;

            return UploadProgressMath.CalculatePercentage(TotalBytesTransferred, TotalSizeBytes, precision);
        }
    }

    internal static class UploadProgressMath
    {
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
