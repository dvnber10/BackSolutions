namespace BackSolutions.Core.Enums;

/// <summary>
/// Severidad del escalamiento. El valor numérico se usa para calcular la prioridad
/// de la conversación (<c>Conversation.HighestSeverity</c>).
/// </summary>
public enum EscalationSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
