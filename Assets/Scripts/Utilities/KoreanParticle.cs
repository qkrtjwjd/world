/// <summary>
/// 한국어 조사 자동 선택 유틸.
/// 앞 단어의 받침 유무에 따라 이/가, 은/는, 을/를, 과/와, 아/야, 으로/로, 이라/라 를 고른다.
/// 플레이어가 정한 이름이 대사에 들어가므로 (PlayerIdentity) 조사를 고정할 수 없다.
/// </summary>
public static class KoreanParticle
{
    private const char HangulStart = '가';   // 가
    private const char HangulEnd   = '힣';   // 힣
    private const int  JongCount   = 28;         // 종성 개수 (없음 포함)
    private const int  JongRieul   = 8;          // 종성 ㄹ 의 인덱스

    /// <summary>
    /// word 뒤에 붙일 조사를 골라 "단어+조사" 를 반환한다.
    /// particle 은 받침 없는 형태로 넘긴다: "가" "는" "를" "와" "야" "로" "라"
    /// 인식하지 못하는 조사는 그대로 이어 붙인다.
    /// </summary>
    public static string Attach(string word, string particle)
        => word + Select(word, particle);

    /// <summary>word 에 맞는 조사만 반환한다 (단어는 붙이지 않는다).</summary>
    public static string Select(string word, string particle)
    {
        if (string.IsNullOrEmpty(particle)) return "";
        if (string.IsNullOrEmpty(word))     return particle;

        bool hasFinal = HasFinalConsonant(word, out bool isRieul);

        switch (particle)
        {
            case "가": case "이":       return hasFinal ? "이" : "가";
            case "는": case "은":       return hasFinal ? "은" : "는";
            case "를": case "을":       return hasFinal ? "을" : "를";
            case "와": case "과":       return hasFinal ? "과" : "와";
            case "야": case "아":       return hasFinal ? "아" : "야";
            // ㄹ 받침은 "으로" 가 아니라 "로" 를 쓴다 (물로, 설로)
            case "로": case "으로":     return hasFinal && !isRieul ? "으로" : "로";
            case "라": case "이라":     return hasFinal ? "이라" : "라";
            case "랑": case "이랑":     return hasFinal ? "이랑" : "랑";
            case "예요": case "이에요": return hasFinal ? "이에요" : "예요";
            default:                    return particle;
        }
    }

    /// <summary>마지막 글자에 받침이 있는지 판정한다. ㄹ 받침이면 isRieul 이 true.</summary>
    public static bool HasFinalConsonant(string word, out bool isRieul)
    {
        isRieul = false;
        if (string.IsNullOrEmpty(word)) return false;

        // 괄호·따옴표 등으로 끝나는 경우를 대비해 뒤에서부터 판정 가능한 글자를 찾는다
        for (int i = word.Length - 1; i >= 0; i--)
        {
            char c = word[i];

            if (c >= HangulStart && c <= HangulEnd)
            {
                int jong = (c - HangulStart) % JongCount;
                isRieul  = jong == JongRieul;
                return jong != 0;
            }

            if (c >= '0' && c <= '9')
                return DigitHasFinal(c, out isRieul);

            if (IsLatinLetter(c))
                return LatinHasFinal(c, out isRieul);
        }

        // 판정할 글자가 하나도 없으면 받침 없음으로 취급
        return false;
    }

    /// <summary>숫자를 한국어로 읽었을 때의 받침. 0영 1일 3삼 6육 7칠 8팔 = 받침 있음.</summary>
    static bool DigitHasFinal(char c, out bool isRieul)
    {
        // 1(일) 7(칠) 8(팔) 은 ㄹ 받침
        isRieul = c == '1' || c == '7' || c == '8';
        switch (c)
        {
            case '0': case '1': case '3': case '6': case '7': case '8': return true;
            default:                                                    return false; // 2 4 5 9
        }
    }

    /// <summary>
    /// 영문 이름의 근사 판정. 한국어로 옮겼을 때 받침으로 끝나는 l·m·n 만 받침 있음으로 본다.
    /// 나머지 자음은 '크·트·스'처럼 모음이 붙어 읽히므로 받침 없음 취급이다 (Mark→마크가, Peter→피터가).
    /// </summary>
    static bool LatinHasFinal(char c, out bool isRieul)
    {
        char lower = char.ToLowerInvariant(c);
        isRieul    = lower == 'l';                   // Bill→빌, 그래서 "빌로"
        return isRieul || lower == 'm' || lower == 'n';
    }

    static bool IsLatinLetter(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
}
