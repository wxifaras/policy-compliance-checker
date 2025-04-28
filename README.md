# Policy Compliance Checker API

The Policy Compliance Checker API is a .NET 9 ASP.NET Core that leverages the power of Azure OpenAI to automatically analyze engagement letters against company policies. The system identifies potential compliance violations and delivers detailed reports with real-time progress updates to end users. This API can serve as a critical tool for organizations looking to ensure contract documents adhere to established policies before finalization. The solution can be adapted to use different types of documents.

# Key Features
- AI-Powered Policy Compliance Analysis: Uses Azure OpenAI (GPT-4o) to detect policy violations in engagement letters
- Document Processing: Extracts and processes text from various document formats using Azure Document Intelligence
- Real-time Progress Updates: Keeps users informed about analysis progress via SignalR
- Comprehensive Violation Reporting: Generates detailed reports highlighting specific policy violations
- Versioned Policy Management: Supports multiple policy document versions
- Asynchronous Processing: Queue-based architecture for handling multiple compliance check requests
- API Versioning: Ensures backward compatibility with client applications

# Technology Stack
- .NET 9: Modern backend framework
- Azure OpenAI: AI model (GPT-4o) for natural language understanding and policy analysis
- Azure Document Intelligence: Document text extraction
- Azure Cosmos DB: Document storage and logging
- Azure Blob Storage: File storage for policies and engagement letters
- Azure Queue Storage: Asynchronous task processing
- Azure SignalR Service: Real-time communication
- Swagger/OpenAPI: API documentation 

# System Architecture
The application follows a microservices-oriented architecture with these key components:  
1.	Controllers: Handle HTTP API requests
2.	Service Layer: Implement business logic interfacing with Azure services
3.	SignalR Hub: Provide real-time updates to client applications
4.	Queue Service: Process compliance checking requests asynchronously

# Key Components:
- PolicyCheckerService: Core service that analyzes documents against policy files
- AzureOpenAIService: Handles interactions with Azure OpenAI
- AzureStorageService: Manages blob storage operations for document storage
- AzureCosmosDBService: Handles database operations for logging and tracking
- PolicyCheckerHub: SignalR hub for real-time updates
- PolicyCheckerQueueService: Background service that processes queued compliance checks

# Configuration
The application requires configuration for various Azure services.  
```json
{
  "AzureOpenAIOptions": {
    // Set MaxTokens to 10–30% less than the model's documented maximum to account for prompt length and token estimation.
    // The actual maximum includes both input and output tokens, and token usage is estimated at request time.
    // For large prompts, reduce this further to avoid exceeding the model's context limit.
    "MaxTokens": 12800,
    "RetryCount": 3,
    "RetryDelayInSeconds": 60,
    "DeploymentName": "<your-openai-deployment-name>",
    "EndPoint": "<your-azure-openai-endpoint>",
    "ApiKey": "<your-azure-openai-api-key>"
  },
  "CosmosDbOptions": {
    "DatabaseName": "",
    "ContainerName": "",
    "AccountUri": "<your-cosmos-db-uri>",
    "TenantId": "<your-tenant-id>"
  },
  "AzureStorageOptions": {
    "PoliciesContainer": "",
    "EngagementsContainer": "",
    "QueueName": "",
    "StorageConnectionString": "<your-storage-connection-string>"
  },
  "AzureDocIntelOptions": {
    "Endpoint": "<your-document-intelligence-endpoint>",
    "ApiKey": "<your-document-intelligence-api-key>"
  },
  "ConnectionStrings": {
    "AzureSignalR": "<your-signalr-connection-string>"
  },
  "ChunkingOptions": {
    "OverlapPercentage": 0.0
  }
}
```

# API Reference
The API is versioned using URL path versioning. The current version is v1.

# Admin API Endpoints
## Policy Management

- POST /api/v1/admin/policy: Upload a new policy document
  - Accepts a form with policy file
  - Returns a version ID for the policy
 
## Log Management
- GET /api/v1/admin/policy-logs: Get policy upload logs
  - Optional user ID filter
- GET /api/v1/admin/engagement-logs: Get engagement letter logs
  - Optional user ID filter
 
## Policy Checking
- POST api/v1/policychecker/enqueue-policy-check
  - Submits an engagement letter to be checked against a policy document
  - Results are delivered asynchronously via SignalR
- GET api/v1/policychecker/get-policies
  - Gets a dictionary of all policies and their versions
 
## SignalR Endpoints
The application exposes a SignalR hub at /policycheckerhub

# Document Processing Flow
1. Upload Policy Document: Admin uploads a policy document that gets stored with version tracking  
2. Submit Engagement Letter: User submits an engagement letter for policy compliance checking  
3. Document Processing: Document Intelligence extracts text content from both documents  
4. Policy Analysis:  
    - Text is tokenized and chunked if necessary  
    - OpenAI analyzes the engagement letter against policy chunks  
    - Real-time progress updates are sent via SignalR

# Performance Considerations
- The system chunks large policy documents to work within OpenAI token limits
- Asynchronous queue-based processing allows scaling to handle multiple requests
- Azure Cosmos DB provides globally distributed, low-latency storage for logs

# Security Notes
- Consider using Managed Identities and updating the code to use DefaultAzureCredential
- Store sensitive configuration values in Azure Key Vault or use User Secrets during development
- The included appsettings.Local.json contains placeholder API keys that should be replaced with actual keys
- Ensure proper CORS and authentication are configured for production deployments
