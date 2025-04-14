using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;

namespace PolicyComplianceCheckerApi.Services;

public class AzureStorageService : IAzureStorageService
{
    private readonly string _storageConnectionString;
    private readonly ILogger<AzureStorageService> _logger;
    private readonly string _policiesContainer;
    private readonly string _engagementsContainerName;

    public AzureStorageService(
        IOptions<AzureStorageOptions> options,
        ILogger<AzureStorageService> logger)
    {
        _storageConnectionString = options.Value.StorageConnectionString;
        _policiesContainer = options.Value.PoliciesContainer;
        _logger = logger;
        _engagementsContainerName = options.Value.EngagementsContainer;
    }

    public async Task UploadFileToEngagementsContainerAsync(BinaryData file, string fileName)
    {
        var blobServiceClient = new BlobServiceClient(_storageConnectionString);
        var blobContainer = blobServiceClient.GetBlobContainerClient(_engagementsContainerName);
        var blobClient = blobContainer.GetBlobClient(fileName);

        _logger.LogInformation($"Uploading File. {fileName}");

        await blobClient.UploadAsync(file, overwrite: true);
    }

    public async Task UploadPolicyAsync(Stream imageStream, string fileName, string version)
    {
        var blobServiceClient = new BlobServiceClient(_storageConnectionString);
        var blobContainer = blobServiceClient.GetBlobContainerClient(_policiesContainer);
        var blobClient = blobContainer.GetBlobClient(fileName);

        _logger.LogInformation($"Uploading Policy File. {fileName}");

        var response = await blobClient.UploadAsync(imageStream, overwrite: true);

        var tags = new Dictionary<string, string>
        {
            { "version", version }
        };

        await blobClient.SetTagsAsync(tags);

        _logger.LogInformation($"tags set for blob: {fileName}, version: {version}");
    }
     
    public async Task<string> GetEngagementSasUriAsync(string fileName)
    {
        var blobClient = new BlobClient(_storageConnectionString, _engagementsContainerName, fileName);

        if (!await blobClient.ExistsAsync())
        {
            _logger.LogError($"Blob does not exist: {fileName} container: {_engagementsContainerName}");
            throw new FileNotFoundException($"Blob does not exist: {fileName} container: {_engagementsContainerName}");
        }

        var sasBuilder = BuildSasUri(fileName, _engagementsContainerName);

        _logger.LogInformation($"Generating SAS URI for {fileName} container: {_engagementsContainerName}");

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return sasUri?.ToString() ?? throw new InvalidOperationException("Failed to generate SAS URI");
    }

    public async Task<string> GetPolicySasUriAsync(string fileName, string version)
    {
        // Construct the tag expression to find the blob with the specified version
        var tagExpression = $"\"version\" = '{version}'";
        var containerClient = new BlobContainerClient(_storageConnectionString, _policiesContainer);

        // Search for blobs matching the tag expression
        await foreach (var blobItem in containerClient.FindBlobsByTagsAsync(tagExpression))
        {
            if (blobItem.BlobName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                // Create a BlobClient for the found blob
                var blobClient = containerClient.GetBlobClient(blobItem.BlobName);

                // Check if the blob exists
                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogError($"Blob does not exist: {fileName} version: {version} container: {_policiesContainer}");
                    throw new FileNotFoundException($"Blob does not exist: {fileName} version: {version} container: {_policiesContainer}");
                }

                // Build the SAS URI
                var sasBuilder = BuildSasUri(fileName, _policiesContainer);
                _logger.LogInformation($"Generating SAS URI for {fileName} in {_policiesContainer}");
                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                return sasUri?.ToString() ?? throw new InvalidOperationException("Failed to generate SAS URI");
            }
        }

        // If no matching blob is found
        _logger.LogError($"Blob with name '{fileName}' and version '{version}' not found in container '{_policiesContainer}'.");
        throw new FileNotFoundException($"Blob with name '{fileName}' and version '{version}' not found in container '{_policiesContainer}'.");
    }

    private static BlobSasBuilder BuildSasUri(string fileName, string container)
    {
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = fileName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return sasBuilder;
    }
}