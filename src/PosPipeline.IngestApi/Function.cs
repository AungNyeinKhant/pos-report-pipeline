using System.Net;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using PosPipeline.Aws;
using PosPipeline.Core.Contracts;
using PosPipeline.Core.Parsing;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PosPipeline.IngestApi;

/// <summary>
/// POST /pos-reports
///
/// Takes the raw POS report as plain text, checks it is well formed enough to identify the
/// flight, stores it under the "pos/" prefix and acknowledges it. Everything after that
/// happens asynchronously, triggered by the S3 object creation.
/// </summary>
public class Function
{
    public async Task<APIGatewayProxyResponse> HandleAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Inside the try: a body that is not valid base64 is a client error, not a fault.
            var rawMessage = ReadBody(request);

            var useCase = PipelineServices.CreateIngestUseCase(context.Logger);
            var result = await useCase.ExecuteAsync(rawMessage);

            // The report is safely stored; parsing and calculation happen asynchronously, which
            // is what the RECEIVED status in the body tells the caller.
            return ApiResponses.Json(
                HttpStatusCode.OK,
                new IngestAcceptedResponse { FlightId = result.FlightId, Status = result.Status });
        }
        catch (PosReportFormatException error)
        {
            // Client error: the message will never become valid, so say why and stop.
            context.Logger.LogWarning($"Rejected POS report: {error.Message}");

            return ApiResponses.Error(HttpStatusCode.BadRequest, error.Message);
        }
        catch (Exception error)
        {
            context.Logger.LogError($"Failed to accept POS report. {error}");

            return ApiResponses.Error(HttpStatusCode.InternalServerError, "Failed to accept the POS report.");
        }
    }

    /// <summary>
    /// API Gateway base64-encodes the body when the content type is treated as binary, so
    /// decode it before handing the text on.
    /// </summary>
    /// <exception cref="PosReportFormatException">The body claims to be base64 but is not.</exception>
    private static string? ReadBody(APIGatewayProxyRequest request)
    {
        if (!request.IsBase64Encoded || request.Body is null)
        {
            return request.Body;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(request.Body));
        }
        catch (FormatException)
        {
            throw new PosReportFormatException("Request body is marked as base64 but could not be decoded.");
        }
    }
}
