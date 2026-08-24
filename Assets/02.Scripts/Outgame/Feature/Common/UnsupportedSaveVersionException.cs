using System;

public sealed class UnsupportedSaveVersionException : Exception
{
    public UnsupportedSaveVersionException(
        string dataName,
        int storedVersion,
        int supportedVersion)
        : base(
            $"{dataName} 저장 버전 {storedVersion}은 현재 지원 버전 " +
            $"{supportedVersion}보다 높습니다.")
    {
    }
}
