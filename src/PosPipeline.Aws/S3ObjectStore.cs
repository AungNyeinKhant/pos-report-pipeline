using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using PosPipeline.Core.Ports;

namespace PosPipeline.Aws;

/// <summary>S3-backed <see cref="IObjectStore"/>. One bucket, three prefixes.</summary>
public sealed class S3ObjectStore : IObjectStore
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public S3ObjectStore(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3;
        _bucketName = bucketName;
    }

    public Task PutTextAsync(string key, string content, string contentType, CancellationToken cancellationToken = default) =>
        _s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                ContentBody = content,
                ContentType = contentType,
            },
            cancellationToken);

    public async Task<string> GetTextAsync(string key, CancellationToken cancellationToken = default)
    {
        using var response = await _s3.GetObjectAsync(_bucketName, key, cancellationToken);
        using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}
