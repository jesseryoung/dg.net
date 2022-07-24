using System.Text.Json;

namespace DG.Net;

public class EventMessage
{
    public string event_name { get; set; } = null!;


    public static EventMessage Deserialize(string message)
    {
        var eventMessage = JsonSerializer.Deserialize<EventMessage>(message);
        if (eventMessage is null)
        {
            throw new InvalidOperationException($"Could not deserialize message. Message: {message}");
        }

        if (!eventMessageTypeMap.ContainsKey(eventMessage.event_name))
        {
            throw new InvalidOperationException($"Uknown event: {eventMessage.event_name}");
        }

        var messageType = eventMessageTypeMap[eventMessage.event_name];
        var actualMessage = JsonSerializer.Deserialize(message, messageType) as EventMessage;

        if (actualMessage is null)
        {
            throw new InvalidOperationException($"Could not deserialize message to {messageType.FullName}. Message: {message}");
        }

        return actualMessage;
    }

    static readonly Dictionary<string, Type> eventMessageTypeMap = new()
    {
        { "player_death", typeof(PlayerDeathEventMessage) },
        { "teamplay_round_start", typeof(TeamplayRoundStartMessage) },
        { "teamplay_round_win", typeof(TeamplayRoundWinMessage) },
        { "object_destroyed", typeof(ObjectDestroyedEventMessage) },
    };
}

public class PlayerDeathEventMessage: EventMessage
{
    public int? userid_client_id { get; set; }
    public int? attacker_client_id { get; set; }
    public int? assister_client_id { get; set; }

    public string? userid_name { get; set; }
    public string? attacker_name { get; set; }
    public string? assister_name { get; set; }

    public string? userid_steam_id { get; set; }
    public string? attacker_steam_id { get; set; }
    public string? assister_steam_id { get; set; }

    public int? damagebits { get; set; }
    public int? death_flags { get; set; }
    public string? weapon { get; set; }
    public string? weapon_logclassname { get; set; }
}

public class ObjectDestroyedEventMessage : PlayerDeathEventMessage
{

}

public class TeamplayRoundStartMessage: EventMessage
{

}

public class TeamplayRoundWinMessage: EventMessage
{
    public int? team { get; set; }
    public int? winreason { get; set; }
}
