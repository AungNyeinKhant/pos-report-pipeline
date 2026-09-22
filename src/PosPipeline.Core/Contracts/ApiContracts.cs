namespace PosPipeline.Core.Contracts;

/// <summary>202-style acknowledgement returned by POST /pos-reports.</summary>
public sealed class IngestAcceptedResponse
{
    public required string FlightId { get; init; }

    /// <summary>Always "RECEIVED": the raw message is stored, downstream processing is asynchronous.</summary>
    public required string Status { get; init; }
}

/// <summary>Body of GET /status/{flightId}.</summary>
public sealed class FlightStatusResponse
{
    public required string FlightId { get; init; }

    /// <summary>Timestamp of the most recent record behind this status.</summary>
    public required string Timestamp { get; init; }

    // Present once a calculation exists for the flight. Until then only the two fields above
    // are returned, which is how a caller can tell the calculator has not caught up yet.
    public int? RemainingFlightTimeMinutes { get; init; }
    public double? EstimatedFuelAtArrivalKg { get; init; }
    public bool? LowFuelWarning { get; init; }
}

/// <summary>Error body returned for 4xx and 5xx responses.</summary>
public sealed class ErrorResponse
{
    public required string Message { get; init; }
}

/// <summary>SQS message handed from the parser to the calculator.</summary>
public sealed class CalculationQueueMessage
{
    public required string FlightId { get; init; }

    /// <summary>POS report timestamp; together with the flight ID it is the parsed record's key.</summary>
    public required string Timestamp { get; init; }
}
