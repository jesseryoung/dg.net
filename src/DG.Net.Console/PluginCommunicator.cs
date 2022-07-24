using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DG.Net;

public class PluginCommunicator
{

    private readonly string consoleLogPath;

    public PluginCommunicator(IOptions<ConsoleOptions> options)
    {
        this.consoleLogPath = options.Value.InputPath;
    }

    public async ValueTask PlayerDrinks(int clientId, string steamId, string[] messages)
    {
        using var file = new FileStream(this.consoleLogPath, FileMode.Open, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
        using var sw = new StreamWriter(file);
        var message = new 
        {
            client_id = clientId,
            steam_id = steamId,
            messages = messages
        };
        
        await sw.WriteLineAsync($"player_drinks {JsonSerializer.Serialize(message)}");

    }
}
