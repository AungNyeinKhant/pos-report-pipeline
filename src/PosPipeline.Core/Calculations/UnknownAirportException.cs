namespace PosPipeline.Core.Calculations;

/// <summary>
/// Raised when a POS report names a destination that is not in <see cref="AirportDirectory"/>.
/// The report cannot be turned into a distance, so it is not retryable.
/// </summary>
public sealed class UnknownAirportException : Exception
{
    public UnknownAirportException(string airportCode)
        : base($"Airport code '{airportCode}' is not in the airport directory.")
    {
        AirportCode = airportCode;
    }

    public string AirportCode { get; }
}
