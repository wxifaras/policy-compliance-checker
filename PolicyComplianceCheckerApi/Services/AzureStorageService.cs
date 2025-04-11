using Azure.Storage.Blobs;
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

    public async Task UploadViolationsFileAsync(BinaryData file, string fileName)
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
        await blobClient.UploadAsync(imageStream, overwrite: true);

        var metadata = new Dictionary<string, string>
        {
            { "version", version }
        };

        await blobClient.SetMetadataAsync(metadata);
        _logger.LogInformation($"Metadata set for blob: {fileName}, version: {version}");
    }
     
    public Task<string> GenerateViolationsSasUriAsync(string fileName) =>
        GenerateSasUriAsync(fileName, _engagementsContainerName);

    public Task<string> GeneratePolicySasUriAsync(string fileName, string version) =>
        GenerateSasUriAsync(fileName, _policiesContainer, version);

    private async Task<string> GenerateSasUriAsync(
    string fileName,
    string containerName,
    string? version = null)
    {
        var blobClient = new BlobClient(_storageConnectionString, containerName, fileName);

        if (!await blobClient.ExistsAsync())
        {
            _logger.LogError("Blob does not exist: {FileName} in container {Container}", fileName, containerName);
            throw new FileNotFoundException($"Blob does not exist: {fileName} in container {containerName}");
        }

        // Version validation for policy documents
        if (version != null)
        {
            var properties = await blobClient.GetPropertiesAsync();
            if (!properties.Value.Metadata.TryGetValue("version", out var actualVersion) || actualVersion != version)
            {
                _logger.LogWarning("Version mismatch. Expected: {Expected}, Actual: {Actual} for {FileName}",
                    version, actualVersion ?? "<none>", fileName);
                throw new InvalidOperationException($"Version mismatch. Expected: {version}, Actual: {actualVersion}");
            }
        }

        var sasBuilder = BuildSasUri(fileName, containerName);
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        _logger.LogInformation("Generating SAS URI for {FileName} in {Container}", fileName, containerName);
        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return sasUri?.ToString() ?? throw new InvalidOperationException("Failed to generate SAS URI");
    }

    private static BlobSasBuilder BuildSasUri(string fileName, string container)
    {
        return new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = fileName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
        };
    }
}