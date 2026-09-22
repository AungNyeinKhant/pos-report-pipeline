namespace PosPipeline.Core.Model;

/// <summary>
/// The identifying fields of a POS report: enough to build the flight ID, and all the
/// ingest API needs before it hands the raw message off to the parser.
/// </summary>
/// <param name="FlightNumber">IATA airline code + flight number, e.g. "UL204".</param>
/// <param name="Departure">Departure airport IATA code, e.g. "RGN".</param>
/// <param name="Destination">Destination airport IATA code, e.g. "BKK".</param>
/// <param name="FlightDate">
/// Day-of-month from the message, combined with the year and month of the receiving system.
/// </param>
public sealed record FlightIdentity(
    string FlightNumber,
    string Departure,
    string Destination,
    DateOnly FlightDate)
{
    /// <summary>
    /// Separator-free flight ID: {flightNumber}{yyyyMMdd}{departure}{destination},
    /// e.g. "UL20420260904RGNBKK".
    /// </summary>
    public string FlightId =>
        $"{FlightNumber}{FlightDate:yyyyMMdd}{Departure}{Destination}";
}
