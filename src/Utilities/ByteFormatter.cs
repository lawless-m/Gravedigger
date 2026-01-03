namespace Gravedigger.Utilities
{
    /// <summary>
    /// Utility class for formatting byte sizes
    /// </summary>
    public static class ByteFormatter
    {
        private static readonly string[] Sizes = { "B", "KB", "MB", "GB", "TB" };

        public static string Format(long bytes)
        {
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < Sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {Sizes[order]}";
        }
    }
}
