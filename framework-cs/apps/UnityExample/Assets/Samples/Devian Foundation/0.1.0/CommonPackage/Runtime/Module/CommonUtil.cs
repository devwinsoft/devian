namespace Devian.Domain.Common
{
    public static class CommonUtil
    {
        public static bool TryGetLanguageCode(string language, out string code)
        {
            code = GetLanguageCodeOrEmpty(language);
            return !string.IsNullOrWhiteSpace(code);
        }

        public static string GetLanguageCodeOrEmpty(string language)
        {
            switch (language)
            {
                case "Korean":
                    return "ko";
                case "English":
                    return "en";
                case "Japanese":
                    return "ja";
                case "ChineseSimplified":
                    return "zh-Hans";
                case "ChineseTraditional":
                    return "zh-Hant";
                case "German":
                    return "de";
                case "French":
                    return "fr";
                case "Spanish":
                    return "es";
                case "Portuguese":
                    return "pt";
                case "Russian":
                    return "ru";
                case "Thai":
                    return "th";
                case "Vietnamese":
                    return "vi";
                case "Indonesian":
                    return "id";
                default:
                    return string.Empty;
            }
        }
    }
}
