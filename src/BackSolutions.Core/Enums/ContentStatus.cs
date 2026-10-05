namespace BackSolutions.Core.Enums;

/// <summary>Ciclo de vida editorial. Los endpoints públicos solo devuelven <see cref="Published"/>.</summary>
public enum ContentStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
