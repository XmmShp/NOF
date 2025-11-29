namespace NOF.SourceGenerator.Tests;

internal static class StringExtensions
{
    extension(string code)
    {
        public string NormalizeLineEndingsAndTrim()
        {
            return code.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        }
    }
}
