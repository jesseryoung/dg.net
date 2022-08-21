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


    public async ValueTask TellPlayer(int clientId, string steamId, bool playDrinkSound = false, bool showInPanel = false, params string[] messages)
    {
        using var file = new FileStream(this.consoleLogPath, FileMode.Open, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
        using var sw = new StreamWriter(file);
        var message = new 
        {
            client_id = clientId,
            steam_id = steamId,
            messages = messages,
            play_drink_sound = playDrinkSound,
            show_in_panel = showInPanel
        };
        
        await sw.WriteLineAsync($"dg_tell_player {JsonSerializer.Serialize(message)}");
    }
}
