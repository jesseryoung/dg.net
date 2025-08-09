using DG.Net;
using DG.Net.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        // Options
        services.AddOptions<ConsoleOptions>().BindConfiguration("ConsoleOptions");
        
        // Services
        services.AddSingleton<PluginCommunicator>();
        services.AddSingleton<EventDispatcher>();
        services.AddPluginEventHandlers();
        services.AddGameRules();

        // Background Service
        services.AddHostedService<Daemon>();
    })
    .Build();


await host.RunAsync();


class Daemon : BackgroundService
{
    private const string messageHeader = "drinkinggame_output: ";
    private readonly string consoleLogPath;
    private readonly EventDispatcher eventDispatcher;
    private readonly ILogger<Daemon> logger;

    public Daemon(IOptions<ConsoleOptions> options, EventDispatcher eventDispatcher, ILogger<Daemon> logger)
    {
        this.consoleLogPath = options.Value.OutputPath;
        this.eventDispatcher = eventDispatcher;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var fs = new FileStream(consoleLogPath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        fs.Seek(0, SeekOrigin.End);
        while (!stoppingToken.IsCancellationRequested)
        {
            var output = await sr.ReadLineAsync();
            
            if (output is not null && output.StartsWith(messageHeader)) // You should test this change!
            {
                var rawMessage = output.Split(messageHeader)[1];
                try
                {
                    await this.eventDispatcher.HandleMessage(rawMessage);
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Failure while handling message");
                }
            }

            await Task.Delay(100);
        }
    }
}