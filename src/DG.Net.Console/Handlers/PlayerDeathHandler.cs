using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DG.Net.Handlers;




public class PlayerDeathHandler : IPluginEventHandler<PlayerDeathEventMessage>
{
    private readonly PluginCommunicator communicator;
    private readonly IEnumerable<IGameRule> rules;
    private readonly ILogger<PlayerDeathHandler> logger;

    public PlayerDeathHandler(PluginCommunicator communicator, IEnumerable<IGameRule> rules, ILogger<PlayerDeathHandler> logger)
    {
        this.communicator = communicator;
        this.rules = rules;
        this.logger = logger;
    }

    public async ValueTask Handle(PlayerDeathEventMessage message)
    {
        if (message.userid_name.IsPlayerDrinking()
            && message.userid_client_id.HasValue
            && message.userid_steam_id is not null)
        {
            int totalDrinks = 0;
            var drinkMessages = new List<string>();
            foreach(var rule in this.rules)
            {
                if (rule.ShouldDrink(message))
                {
                    var drinks = rule.CalculateDrinks(message);
                    totalDrinks += drinks;
                    drinkMessages.Add($"[+{drinks}] {rule.BuildMessage(message)}");
                }
            }

            if (totalDrinks > 0)
            {
                drinkMessages.Add($"Total: {totalDrinks}");
                await this.communicator.PlayerDrinks(message.userid_client_id.Value, message.userid_steam_id, drinkMessages.ToArray());
            }
        }
    }

}

public static class PlayerExtensions
{
    public static bool IsPlayerDrinking(this string? playerName) 
    {
        if (playerName is null)
        {
            return false;
        }

        return Regex.Match(playerName, @"[\[\{][SD]C?G[\]\}]", RegexOptions.IgnoreCase).Success;       
    }
}


public interface IGameRule
{
    bool ShouldDrink(PlayerDeathEventMessage message);
    int CalculateDrinks(PlayerDeathEventMessage message);
    string BuildMessage(PlayerDeathEventMessage message);
}

// Rules
// +1 If your killer is drinking
// +1 If the assister is drinking(unless it's a suicide)
// +1 If both attacker and assister are drinking
// +n If the killer kills you with a special weapon (only if it isn't a backstab)
// +2 If the assister dominates/revenge you
// +2 If the killer dominates/revenge
// +6 If it's a vehicle
// +6 If it's a taunt kill

public class KillerDrinkingRule : IGameRule
{
    public string BuildMessage(PlayerDeathEventMessage message) => "You were killed by [DG]";
    public int CalculateDrinks(PlayerDeathEventMessage message) => 1;
    public bool ShouldDrink(PlayerDeathEventMessage message) => message.attacker_name.IsPlayerDrinking() && message.attacker_client_id != message.userid_client_id;
}

public class AssisterDrinkingRule : IGameRule
{
    public string BuildMessage(PlayerDeathEventMessage message) => "You were kill assisted by [DG]";
    public int CalculateDrinks(PlayerDeathEventMessage message) => 1;
    public bool ShouldDrink(PlayerDeathEventMessage message) => message.assister_name.IsPlayerDrinking() && message.attacker_client_id != message.userid_client_id;
}