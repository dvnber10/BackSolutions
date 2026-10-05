namespace BackSolutions.Core.Enums;

/// <summary>Define de quién es el turno de respuesta en una conversación.</summary>
public enum ConversationStatus
{
    Open = 0,
    AwaitingClient = 1,
    AwaitingTeam = 2,
    Closed = 3
}
