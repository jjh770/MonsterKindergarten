// 단어의 마지막 글자에 받침이 있는지에 따라 조사를 고른다.
// "범퍼을(를)"처럼 둘을 겹쳐 쓰면 화면에 괄호가 그대로 나오므로 문구를 만들 때 이 쪽을 쓴다.
public static class KoreanParticle
{
    // 을/를. 한글이 아닌 글자로 끝나면 받침이 없는 것으로 본다.
    public static string Object(string word)
    {
        return word + (HasFinalConsonant(word) ? "을" : "를");
    }

    private static bool HasFinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        char last = word[word.Length - 1];
        const char firstSyllable = '가';
        const char lastSyllable = '힣';
        if (last < firstSyllable || last > lastSyllable) return false;

        // 한글 음절은 (초성 × 21 + 중성) × 28 + 종성 순서로 놓여 있다. 종성 0이 받침 없음이다.
        return (last - firstSyllable) % 28 != 0;
    }
}
