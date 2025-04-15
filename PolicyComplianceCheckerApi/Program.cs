using Asp.Versioning;
using concierge_agent_api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

// Load configuration from appsettings.json and appsettings.local.json
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
}).AddMvc() // This is needed for controllers
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddOptions<AzureOpenAIOptions>()
           .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.AzureOpenAI))
           .ValidateDataAnnotations();

builder.Services.AddOptions<AzureStorageOptions>()
           .Bind(builder.Configuration.GetSection(AzureStorageOptions.AzureStorage))
           .ValidateDataAnnotations();

builder.Services.AddOptions<CosmosDbOptions>()
           .Bind(builder.Configuration.GetSection(CosmosDbOptions.CosmosDb))
           .ValidateDataAnnotations();

builder.Services.AddOptions<AzureDocIntelOptions>()
           .Bind(builder.Configuration.GetSection(AzureDocIntelOptions.AzureDocIntel))
           .ValidateDataAnnotations();

builder.Services.AddSingleton<IAzureCosmosDBService>(sp =>
{
    var cosmosDbOptions = sp.GetRequiredService<IOptions<CosmosDbOptions>>();
    var logger = sp.GetRequiredService<ILogger<AzureCosmosDBService>>();
    return new AzureCosmosDBService(cosmosDbOptions, logger);
});

builder.Services.AddSingleton<IAzureStorageService>(sp =>
{
    var azureStorageOptions = sp.GetRequiredService<IOptions<AzureStorageOptions>>();
    var logger = sp.GetRequiredService<ILogger<AzureStorageService>>();
    return new AzureStorageService(azureStorageOptions, logger);
});

builder.Services.AddSingleton<IAzureOpenAIService>(sp =>
{
    var azureOpenAIOptions = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>();
    var logger = sp.GetRequiredService<ILogger<AzureOpenAIService>>();

    return new AzureOpenAIService(azureOpenAIOptions, logger);
});

builder.Services.AddSingleton<IPolicyCheckerService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PolicyCheckerService>>();
    var azureOpenAIService = sp.GetRequiredService<IAzureOpenAIService>();
    var azureStorageService = sp.GetRequiredService<IAzureStorageService>();
    var azureDocIntelOptions = sp.GetRequiredService<IOptions<AzureDocIntelOptions>>();
    var cosmosDbService = sp.GetRequiredService<IAzureCosmosDBService>();

    return new PolicyCheckerService(logger, azureOpenAIService, azureStorageService, azureDocIntelOptions, cosmosDbService);
});

builder.Services.AddHostedService<PolicyCheckerQueueService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Policy Compliance Checker API V1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at the root
    });
}

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();