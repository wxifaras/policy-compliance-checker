using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;
using PolicyComplianceCheckerApi.Models;

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

    public async Task<string> UploadPolicyAsync(Stream imageStream, string fileName)
    {
        var blobServiceClient = new BlobServiceClient(_storageConnectionString);
        var blobContainer = blobServiceClient.GetBlobContainerClient(_policiesContainer);
        var blobClient = blobContainer.GetBlobClient(fileName);

        _logger.LogInformation($"Uploading Policy File. {fileName}");

        var response = await blobClient.UploadAsync(imageStream, overwrite: true);

        _logger.LogInformation($"tags set for blob: {fileName}, version: {response.Value.VersionId}");

        return response.Value.VersionId;
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

    public async Task<string> GetPolicySasUriAsync(string fileName, string versionId)
    {
        var blobClient = new BlobClient(_storageConnectionString, _policiesContainer, fileName);
        var versionedBlobClient = blobClient.WithVersion(versionId);

        if (!await versionedBlobClient.ExistsAsync())
        {
            _logger.LogError($"Blob does not exist: {fileName} container: {_policiesContainer} versionId: {versionId}");
            throw new FileNotFoundException($"Blob does not exist: {fileName} container: {_policiesContainer} versionId: {versionId}");
        }

        var sasBuilder = BuildSasUri(fileName, _policiesContainer);

        _logger.LogInformation($"Generating SAS URI for {fileName} container: {_policiesContainer} versionId: {versionId}");

        var sasUri = versionedBlobClient.GenerateSasUri(sasBuilder);

        return sasUri?.ToString() ?? throw new InvalidOperationException("Failed to generate SAS URI");
    }

    public async Task<Dictionary<string, List<PolicesWithVersionsResponse>>> GetPoliciesWithVersionsAsync()
    {
        var containerClient = new BlobContainerClient(_storageConnectionString, _policiesContainer);

        var policiesWithVersions = new Dictionary<string, List<PolicesWithVersionsResponse>>();

        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.Version))
        {
            var versionInfo = new PolicesWithVersionsResponse
            {
                VersionId = blobItem.VersionId,
                IsLatestVersion = blobItem.IsLatestVersion,
                LastModified = blobItem.Properties.LastModified
            };

            if (!policiesWithVersions.TryGetValue(blobItem.Name, out var versions))
            {
                versions = new List<PolicesWithVersionsResponse>();
                policiesWithVersions[blobItem.Name] = versions;
            }

            versions.Add(versionInfo);
        }

        return policiesWithVersions;
    }

    public async Task<BinaryData> ConvertSasUriToBinaryData(string sasUri)
    {
        var blobClient = new BlobClient(new Uri(sasUri));

        BlobDownloadResult result = await blobClient.DownloadContentAsync();

        return result.Content;
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