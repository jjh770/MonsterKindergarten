namespace Utility
{
    public static class NumberFormatExtension
    {
        // 확장 메서드
        // 이미 존재하는 클래스에 메서드를 추가하는 C#의 독특한 기능 
        // static 클래스가 필요함.
        private static string[] _suffixes =
        {
            "", "K", "M", "B", "T"
        };

        // double 앞에 this가 필요함 -> double에 ToFormattedString이라는 메서드가 추가됨.
        public static string ToForamttedString(this double num)
        {
            if (num < 1000)
            {
                return num.ToString("N0");
            }
            int suffixIndex = 0;

            double value = num;
            // 숫자가 너무 크면 벗어날 수 있기 때문에 조건 하나 더 붙임
            while (value >= 1000 && suffixIndex < _suffixes.Length - 1)
            {
                value /= 1000;
                suffixIndex++;
            }
            if (value >= 100) return $"{value:F0}{_suffixes[suffixIndex]}";
            if (value >= 10) return $"{value:F1}{_suffixes[suffixIndex]}";
            return $"{value:F2}{_suffixes[suffixIndex]}";
        }
    }
}
