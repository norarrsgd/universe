namespace Universe.Exception;

/// <summary></summary>
[Serializable]
public sealed class UniverseException : System.Exception
{
    private const string HelpUrl = "https://github.com/norarrsgd/universe/issues";

    /// <summary>Custom exception</summary>
    public UniverseException()
        => HelpLink = HelpUrl;

    /// <summary>Custom exception</summary>
    public UniverseException(string message) : base(message)
        => HelpLink = HelpUrl;

    /// <summary>Custom exception</summary>
    public UniverseException(string message, System.Exception innerException) : base(message, innerException)
        => HelpLink = HelpUrl;
}