using System.Globalization;

namespace WorkDelta.Core.Localization;

internal static class CoreText
{
    private static bool IsChinese => CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    public static string Choose(string chinese, string english) => IsChinese ? chinese : english;
    public static string Format(string chinese, string english, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Choose(chinese, english), args);
}
