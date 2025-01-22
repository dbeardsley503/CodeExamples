// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Register your hosted service
        services.AddHostedService<WorkerService>();
        
        // Register your custom services
        services.AddSingleton<IStorageService, LocalStorageService>();
        services.AddSingleton<IDataProcessor, DataProcessor>();
        
        // You can later swap LocalStorageService with AzureStorageService
        // services.AddSingleton<IStorageService, AzureStorageService>();
    })
    .ConfigureLogging((hostContext, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        
        // Optional: Add debug output
        logging.AddDebug();
        
        // Later you can add Azure App Insights
        // logging.AddApplicationInsights();
    })
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false)
              .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true)
              .AddEnvironmentVariables();
              
        // Later you can add Azure Key Vault
        // config.AddAzureKeyVault(...);
    })
    .Build();

await host.RunAsync();

// WorkerService.cs
public class WorkerService : BackgroundService
{
    private readonly ILogger<WorkerService> _logger;
    private readonly IStorageService _storageService;
    private readonly IDataProcessor _dataProcessor;
    private readonly IConfiguration _configuration;
    
    public WorkerService(
        ILogger<WorkerService> logger,
        IStorageService storageService,
        IDataProcessor dataProcessor,
        IConfiguration configuration)
    {
        _logger = logger;
        _storageService = storageService;
        _dataProcessor = dataProcessor;
        _configuration = configuration;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            
            try
            {
                var data = await _storageService.GetDataAsync();
                await _dataProcessor.ProcessAsync(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing data");
            }
            
            // Get interval from configuration
            var interval = _configuration.GetValue<int>("WorkerSettings:IntervalMinutes");
            await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
        }
    }
}

// IStorageService.cs
public interface IStorageService
{
    Task<string> GetDataAsync();
    Task SaveDataAsync(string data);
}

// LocalStorageService.cs
public class LocalStorageService : IStorageService
{
    private readonly ILogger<LocalStorageService> _logger;
    private readonly string _localPath;
    
    public LocalStorageService(ILogger<LocalStorageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _localPath = configuration.GetValue<string>("Storage:LocalPath");
    }
    
    public async Task<string> GetDataAsync()
    {
        _logger.LogInformation("Reading data from local storage at {path}", _localPath);
        return await File.ReadAllTextAsync(_localPath);
    }
    
    public async Task SaveDataAsync(string data)
    {
        _logger.LogInformation("Saving data to local storage at {path}", _localPath);
        await File.WriteAllTextAsync(_localPath, data);
    }
}

// IDataProcessor.cs
public interface IDataProcessor
{
    Task ProcessAsync(string data);
}

// DataProcessor.cs
public class DataProcessor : IDataProcessor
{
    private readonly ILogger<DataProcessor> _logger;
    
    public DataProcessor(ILogger<DataProcessor> logger)
    {
        _logger = logger;
    }
    
    public async Task ProcessAsync(string data)
    {
        _logger.LogInformation("Processing data of length: {length}", data?.Length ?? 0);
        // Your processing logic here
        await Task.CompletedTask;
    }
}

// appsettings.json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information"
        }
    },
    "WorkerSettings": {
        "IntervalMinutes": 5
    },
    "Storage": {
        "LocalPath": "C:\\temp\\data.txt"
    }
}
