using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using PosPipeline.Core.Contracts;

namespace PosPipeline.StatusApi;

/// <summary>Builds the JSON responses this API returns.</summary>
internal static class ApiResponses
{
    internal static APIGatewayProxyResponse Json<T>(HttpStatusCode statusCode, T body) => new()
    {
        StatusCode = (int)statusCode,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonDefaults.Serialize(body),
    };

    internal static APIGatewayProxyResponse Error(HttpStatusCode statusCode, string message) =>
        Json(statusCode, new ErrorResponse { Message = message });
}
