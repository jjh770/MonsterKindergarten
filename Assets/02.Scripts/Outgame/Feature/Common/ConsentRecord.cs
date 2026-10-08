using System;
using System.Globalization;
using UnityEngine;

// 이용약관, 개인정보 수집·이용, 만 14세 이상 확인에 동의했는지를 이 기기에 기록한다. 기획서 §21.11.
//
// 계정이 아니라 기기 기준이다. 동의는 로그인보다 먼저 받으므로 아직 계정을 모르고, 다른 기기에서 같은 계정으로
// 들어와도 그 기기에서 동의를 받는다. 세이브 문서와 분리되어 있어 세이브 스키마에는 영향이 없다.
//
// 약관 내용이 바뀌어 다시 동의가 필요하면 CurrentVersion을 올린다. 저장된 버전과 다르면 동의하지 않은 것으로 본다.
// 동의는 철회해도 일부만 되지 않는다. 철회는 계정 삭제이고, 삭제가 끝나면 기록도 지운다.
public static class ConsentRecord
{
    public const int CurrentVersion = 1;

    private const string VersionKey = "Consent_Version";
    private const string AgreedAtKey = "Consent_AgreedAtUtc";

    public static bool HasConsented => PlayerPrefs.GetInt(VersionKey, 0) == CurrentVersion;

    // 마지막으로 동의한 시각이다. 기록이 없거나 읽을 수 없으면 MinValue다.
    public static DateTime AgreedAtUtc
    {
        get
        {
            string stored = PlayerPrefs.GetString(AgreedAtKey, string.Empty);
            return DateTime.TryParse(
                stored,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }
    }

    public static void Record()
    {
        PlayerPrefs.SetInt(VersionKey, CurrentVersion);
        PlayerPrefs.SetString(AgreedAtKey, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(VersionKey);
        PlayerPrefs.DeleteKey(AgreedAtKey);
        PlayerPrefs.Save();
    }
}
