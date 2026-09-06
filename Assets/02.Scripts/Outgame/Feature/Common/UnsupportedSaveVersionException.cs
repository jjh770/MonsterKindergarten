using System;

public sealed class UnsupportedSaveVersionException : Exception
{
    public UnsupportedSaveVersionException(
        string dataName,
        int storedVersion,
        int supportedVersion)
        : base(BuildMessage(dataName, storedVersion, supportedVersion))
    {
    }

    // 저장소는 예외를 던지지 않고 SaveLoadResult.Failed로 올려 보내므로
    // 문구만 필요하다. 도메인마다 다른 문장을 쓰지 않도록 여기서 만든다.
    public static string BuildMessage(
        string dataName,
        int storedVersion,
        int supportedVersion)
    {
        return $"{dataName} 저장 버전 {storedVersion}은 현재 지원 버전 " +
               $"{supportedVersion}보다 높습니다.";
    }
}
