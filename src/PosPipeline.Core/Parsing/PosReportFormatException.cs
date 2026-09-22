namespace PosPipeline.Core.Parsing;

/// <summary>
/// Raised when a POS report does not match the expected wire format. The ingest API turns
/// this into a 400; the parser Lambda treats it as a poison message and does not retry.
/// </summary>
public sealed class PosReportFormatException : Exception
{
    public PosReportFormatException(string message) : base(message)
    {
    }
}
