using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;

namespace PolicyComplianceCheckerApi.Services;

public class AzureStorageService : IAzureStorageService
{
    private readonly string _storageConnectionString;
    private readonly ILogger<AzureStorageService> _logger;
    private readonly string _containerName;

    public AzureStorageService(IOptions<AzureStorageOptions> options,
        ILogger<AzureStorageService> logger)
    {
        _storageConnectionString = options.Value.StorageConnectionString;
        _containerName = options.Value.PoliciesContainer;
        _logger = logger;
    }

    public async Task UploadPolicyAsync(Stream imageStream, string fileName, string version)
    {
        var blobServiceClient = new BlobServiceClient(_storageConnectionString);
        var blobContainer = blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = blobContainer.GetBlobClient(fileName);

        _logger.LogInformation($"Uploading Policy File. {fileName}");
        await blobClient.UploadAsync(imageStream, overwrite: true);

        var metadata = new Dictionary<string, string>
        {
            { "version", version }
        };

        await blobClient.SetMetadataAsync(metadata);
        _logger.LogInformation($"Metadata set for blob: {fileName}, version: {version}");
    }

    public async Task<string> GenerateSasUriAsync(string fileName, string version)
    {
        Uri? sasUri = null;
        var blobClient = new BlobClient(_storageConnectionString, _containerName, fileName);

        if (await blobClient.ExistsAsync())
        {
            // Fetch blob properties to validate metadata
            var properties = await blobClient.GetPropertiesAsync();
            var metadata = properties.Value.Metadata;

            // Check if the expected version matches the metadata
            if (metadata.TryGetValue("version", out var actualVersion) && actualVersion == version)
            {
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _containerName,
                    BlobName = fileName,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);
                _logger.LogInformation("Generating SAS URI for file: {FileName}", fileName);
                sasUri = blobClient.GenerateSasUri(sasBuilder);
            }
            else
            {
                _logger.LogWarning("Blob metadata version mismatch. Expected: {version}, Actual: {ActualVersion}", version, actualVersion);
                throw new Exception($"Blob metadata version mismatch. Expected: {version}, Actual: {actualVersion}");
            }
        }
        else
        {
            _logger.LogError("Blob does not exist: {FileName}", fileName);
            throw new Exception($"Blob does not exist: {fileName}");
        }

        if (sasUri == null)
        {
            _logger.LogError("Failed to generate SAS URI");
            throw new Exception("Failed to generate SAS URI");
        }

        return sasUri.ToString();
    }
}