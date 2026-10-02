namespace Utility
{
    public static class NumberFormatExtension
    {
        // 확장 메서드
        // 이미 존재하는 클래스에 메서드를 추가하는 C#의 독특한 기능 
        // static 클래스가 필요함.
        // 1000배마다 하나씩 쓴다.
        private static readonly string[] _fixedSuffixes =
        {
            "", "K", "M", "B", "T"
        };

        // double 최대값이 약 1.8e308이라 1000배 눈금으로 103단계면 넘어설 수 없다.
        // 상한이 없으면 값이 무한대일 때 나눗셈이 끝나지 않는다.
        private const int MaxSuffixIndex = 103;

        // double 앞에 this가 필요함 -> double에 ToFormattedString이라는 메서드가 추가됨.
        public static string ToFormattedString(this double num)
        {
            if (num < 1000)
            {
                return num.ToString("N0");
            }

            int suffixIndex = 0;
            double value = num;
            while (value >= 1000 && suffixIndex < MaxSuffixIndex)
            {
                value /= 1000;
                suffixIndex++;
            }

            int places = PlacesFor(value);
            double rounded = System.Math.Round(value, places, System.MidpointRounding.AwayFromZero);

            // 반올림 결과가 1000 이상이면 한 단위 위로 올린다. "1000K" 대신 "1.00M".
            if (rounded >= 1000d && suffixIndex < MaxSuffixIndex)
            {
                value = rounded / 1000d;
                suffixIndex++;
                places = PlacesFor(value);
                rounded = System.Math.Round(value, places, System.MidpointRounding.AwayFromZero);
            }
            else
            {
                // 반올림이 100/10 경계를 넘었으면 자릿수를 다시 정한다(예: 99.95→100.0→100).
                value = rounded;
                places = PlacesFor(value);
            }

            string suffix = GetSuffix(suffixIndex);
            return $"{rounded.ToString("F" + places)}{suffix}";
        }

        // 값 크기에 따른 표시 소수 자릿수. 100 이상 0자리, 10 이상 1자리, 그 외 2자리.
        private static int PlacesFor(double v) => v >= 100d ? 0 : v >= 10d ? 1 : 2;

        // T 다음은 aa, ab, ... az, ba 순으로 이어 간다.
        //
        // Qa와 Qi처럼 첫 글자가 겹치는 축약을 쓰면 어느 쪽이 큰지 헷갈린다.
        // 알파벳 순서가 곧 크기 순서라 처음 보는 플레이어도 대소를 바로 안다.
        // 목록을 늘리지 않아도 계속 확장된다.
        private static string GetSuffix(int index)
        {
            if (index < _fixedSuffixes.Length) return _fixedSuffixes[index];

            int order = index - _fixedSuffixes.Length;
            char high = (char)('a' + order / 26);
            char low = (char)('a' + order % 26);
            return $"{high}{low}";
        }
    }
}
