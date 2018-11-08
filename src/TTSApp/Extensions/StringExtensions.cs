namespace TTSApp.Extensions {
    public static class StringExtensions {
        public static string Clip(this string value, int maxLength) {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
