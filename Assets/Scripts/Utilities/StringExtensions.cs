public static class StringExtensions
{
    public static string ConvertFolderSeperators(this string path)
    {
        return path.Replace("\\\\", "\\").Replace("/", "\\");
    }
}