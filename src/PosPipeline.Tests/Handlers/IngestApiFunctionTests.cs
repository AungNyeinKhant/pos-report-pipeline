using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using PosPipeline.Core.Contracts;
using Xunit;

namespace PosPipeline.Tests.Handlers;

/// <summary>
/// Covers the handler boundary the use case tests cannot reach: turning an API Gateway event
/// into a request the pipeline can act on.
///
/// Only the rejection path that short-circuits before any AWS client is created is tested
/// here; everything past that point is covered by the use case tests.
/// </summary>
public class IngestApiFunctionTests
{
    [Fact]
    public async Task Rejects_a_body_that_claims_to_be_base64_but_is_not()
    {
        var request = new APIGatewayProxyRequest
        {
            IsBase64Encoded = true,
            Body = "this is not base64 !!",
        };

        var response = await new IngestApi.Function().HandleAsync(request, new TestLambdaContext());

        // A bad body is the caller's problem, so it must not surface as a fault.
        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("base64", JsonDefaults.Deserialize<ErrorResponse>(response.Body).Message);
    }

    private sealed class TestLambdaContext : ILambdaContext
    {
        public string AwsRequestId => "test-request";
        public IClientContext ClientContext => null!;
        public string FunctionName => "IngestApi";
        public string FunctionVersion => "$LATEST";
        public ICognitoIdentity Identity => null!;
        public string InvokedFunctionArn => string.Empty;
        public ILambdaLogger Logger { get; } = new TestLambdaLogger();
        public string LogGroupName => string.Empty;
        public string LogStreamName => string.Empty;
        public int MemoryLimitInMB => 512;
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(30);
    }

    private sealed class TestLambdaLogger : ILambdaLogger
    {
        public void Log(string message)
        {
        }

        public void LogLine(string message)
        {
        }
    }
}
