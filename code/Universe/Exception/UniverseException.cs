namespace Universe.Exception;

/// <summary></summary>
[Serializable]
public sealed class UniverseException : SystemException
{
    private const string HelpUrl = "https://github.com/kuromukira/simple-cosmos/issues";

    /// <summary>Custom exception</summary>
    public UniverseException()
        => HelpLink = HelpUrl;

    /// <summary>Custom exception</summary>
    public UniverseException(string message) : base(message)
        => HelpLink = HelpUrl;

    /// <summary>Custom exception</summary>
    public UniverseException(string message, SystemException innerException) : base(message, innerException)
        => HelpLink = HelpUrl;
}