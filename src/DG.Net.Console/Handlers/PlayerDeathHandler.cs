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
            foreach (var rule in this.rules)
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
                await this.communicator.TellPlayer(
                    message.userid_client_id.Value,
                    message.userid_steam_id,
                    playDrinkSound: true,
                    showInPanel: true,
                    messages: drinkMessages.ToArray()
                );

                if (message.attacker_client_id.HasValue
                    && message.attacker_steam_id != null
                    && message.attacker_name.IsPlayerDrinking())
                {
                    await this.communicator.TellPlayer(
                        message.attacker_client_id.Value,
                        message.attacker_steam_id,
                        messages: $"You made {message.userid_name} drink {totalDrinks}"
                    );
                }

                if (message.assister_client_id.HasValue
                    && message.assister_steam_id != null
                    && message.assister_name.IsPlayerDrinking())
                {
                    await this.communicator.TellPlayer(
                        message.assister_client_id.Value,
                        message.assister_steam_id,
                        messages: $"You made {message.userid_name} drink {totalDrinks}"
                    );
                }
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

public class SynergyDrinkingRule : IGameRule
{
    public string BuildMessage(PlayerDeathEventMessage message) => "Drinker synergy bonus";
    public int CalculateDrinks(PlayerDeathEventMessage message) => 1;
    public bool ShouldDrink(PlayerDeathEventMessage message) => message.attacker_name.IsPlayerDrinking() && message.assister_name.IsPlayerDrinking() && message.attacker_client_id != message.userid_client_id;
}

public class TauntDrinkingRule : IGameRule
{
    public string BuildMessage(PlayerDeathEventMessage message) => "Killed by a taunt kill";
    public int CalculateDrinks(PlayerDeathEventMessage message) => 6;
    public bool ShouldDrink(PlayerDeathEventMessage message) => message.attacker_name.IsPlayerDrinking() && message.weapon?.Contains("taunt") == true;
}

public class TrainDrinkingRule : IGameRule
{
    const byte DMG_VEHICLE = 1 << 4;
    public string BuildMessage(PlayerDeathEventMessage message) => "You got run over by a train";
    public int CalculateDrinks(PlayerDeathEventMessage message) => 6;
    public bool ShouldDrink(PlayerDeathEventMessage message) => message.damagebits.HasValue && (message.damagebits.Value & DMG_VEHICLE) == DMG_VEHICLE;
}

/*
#define TF_DEATHFLAG_KILLERDOMINATION   (1 << 0)
#define TF_DEATHFLAG_ASSISTERDOMINATION (1 << 1)
#define TF_DEATHFLAG_KILLERREVENGE      (1 << 2)
#define TF_DEATHFLAG_ASSISTERREVENGE    (1 << 3)
#define TF_DEATHFLAG_FIRSTBLOOD         (1 << 4)
#define TF_DEATHFLAG_DEADRINGER         (1 << 5)
*/

public class AttackerDominatedDrinkingRule : IGameRule
{
    const byte TF_DEATHFLAG_KILLERDOMINATION = (1 << 0);
    const byte TF_DEATHFLAG_KILLERREVENGE = (1 << 2);
    const byte DMG_VEHICLE = 1 << 4;
    public string BuildMessage(PlayerDeathEventMessage message) => "Attacker dominated/revenged you";
    public int CalculateDrinks(PlayerDeathEventMessage message) => 2;
    public bool ShouldDrink(PlayerDeathEventMessage message)
    {
        if (message.death_flags.HasValue && message.attacker_name.IsPlayerDrinking())
        {
            var dominated = (message.death_flags.Value & TF_DEATHFLAG_KILLERDOMINATION) == TF_DEATHFLAG_KILLERDOMINATION;
            var revenged = (message.death_flags.Value & TF_DEATHFLAG_KILLERREVENGE) == TF_DEATHFLAG_KILLERREVENGE;
            return dominated || revenged;
        }
        else
        {
            return false;
        }
    }
}

public class AssisterDominatedDrinkingRule : IGameRule
{
    const byte TF_DEATHFLAG_ASSISTERDOMINATION = 1 << 1;
    const byte TF_DEATHFLAG_ASSISTERREVENGE = 1 << 3;
    const byte DMG_VEHICLE = 1 << 4;
    public string BuildMessage(PlayerDeathEventMessage message) => "Assister dominated/revenged you";
    public int CalculateDrinks(PlayerDeathEventMessage message) => 2;
    public bool ShouldDrink(PlayerDeathEventMessage message)
    {
        if (message.death_flags.HasValue && message.assister_name.IsPlayerDrinking())
        {
            var dominated = (message.death_flags.Value & TF_DEATHFLAG_ASSISTERDOMINATION) == TF_DEATHFLAG_ASSISTERDOMINATION;
            var revenged = (message.death_flags.Value & TF_DEATHFLAG_ASSISTERREVENGE) == TF_DEATHFLAG_ASSISTERREVENGE;
            return dominated || revenged;
        }
        else
        {
            return false;
        }
    }
}

public class SpecialWeaponDrinkingRule : IGameRule
{
    private static readonly Dictionary<string, int> drinkMap = new()
    {
        { "amputator", 2 },
        { "armageddon", 6},
        { "axtinguisher", 2 },
        { "back_scratcher", 2 },
        { "ball", 6 },
        { "bat_wood", 2 },
        { "bat", 2 },
        { "battleaxe", 2 },
        { "battleneedle", 2 },
        { "bleed_kill", 3 },
        { "bonesaw", 2 },
        { "boston_basher", 2 },
        { "bottle", 2 },
        { "bushwacka", 2 },
        { "candy_cane", 2 },
        { "claidheamohmor", 2 },
        { "club", 2 },
        { "deflect_arrow", 10 },
        { "deflect_rocket", 4 },
        { "demokatana", 2 },
        { "demoshield", 3 },
        { "disciplinary_action", 2 },
        { "eternal_reward", 2 },
        { "eviction_notice", 2 },
        { "fireaxe", 2 },
        { "fists", 2 },
        { "fryingpan", 2 },
        { "gloves_running_urgently", 2 },
        { "gloves", 2 },
        { "headtaker", 2 },
        { "holiday_punch", 2 },
        { "holy_mackerel", 4 },
        { "holymackerel", 5 },
        { "hot_hand", 5},
        { "knife", 2 },
        { "lava_axe", 2 },
        { "lava_bat", 2 },
        { "mailbox", 2 },
        { "market_gardener", 3 },
        { "nessieclub", 5},
        { "nonnonviolent_protest", 3 },
        { "paintrain", 2 },
        { "persian_persuader", 2 },
        { "pickaxe", 2 },
        { "powerjack", 3 },
        { "robot_arm_combo_kill", 2 },
        { "robot_arm_kill", 2 },
        { "robot_arm", 2 },
        { "sandman", 2 },
        { "shahanshah", 2 },
        { "sharp_dresser", 2 },
        { "shovel", 2 },
        { "sledgehammer", 2 },
        { "solemn_vow", 2 },
        { "southern_comfort_kill", 2 },
        { "southern_hospitality", 2 },
        { "splendid_screen", 3 },
        { "spy_cicle", 2 },
        { "steel_fists", 2 },
        { "sword", 2 },
        { "the_maul", 2 },
        { "thirddegree", 2 },
        { "tribalkukri", 2 },
        { "ubersaw", 2 },
        { "ullapool_caber_explosion", 2 },
        { "ullapool_caber", 3 },
        { "warfan", 20 },
        { "warrior_spirit", 2 },
        { "world", 1 },
        { "wrap_assassin", 10 },
        { "wrench_jag", 2 },
        { "wrench", 2 },
        { "annihilator", 2 },
        { "tf_pumpkin_bomb", 10 },
        { "telefrag", 20 },
        { "mantreads", 20 },
        { "atomizer", 2 }
    };

    public string BuildMessage(PlayerDeathEventMessage message) => "Killed by a special weapon";

    public int CalculateDrinks(PlayerDeathEventMessage message) => drinkMap[message.weapon!];

    public bool ShouldDrink(PlayerDeathEventMessage message) => message.attacker_name.IsPlayerDrinking() && message.weapon != null && drinkMap.ContainsKey(message.weapon);
}