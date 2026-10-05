using BackSolutions.Core.Dtos.Auth;

namespace BackSolutions.Core.Dtos.Admin;

public sealed record UserListItemDto(
    Guid Id,
    string Email,
    string FullName,
    string? AvatarUrl,
    bool IsActive,
    bool MustChangePassword,
    bool IsLockedOut,
    IReadOnlyList<string> Roles,
    int OpenConversations,
    int AssignedLeads,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string FullName,
    IReadOnlyList<string>? Roles,
    string? Phone,
    string? AvatarUrl,
    bool MustChangePassword = true);

public sealed record UpdateUserRequest(
    string? FullName,
    string? Phone,
    string? AvatarUrl,
    bool? IsActive);

public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);

public sealed record ResetPasswordRequest(string NewPassword, bool MustChangePassword = true);

public sealed record RoleDto(Guid Id, string Name, string Description, bool IsSystem, int UserCount);

/// <summary>
/// Métricas de la home del panel. Se calculan en una sola pasada para que la pantalla
/// no dispare cinco consultas separadas al abrirse.
/// </summary>
public sealed record DashboardStatsDto(
    int TotalLeads,
    int NewLeads,
    int LeadsInReview,
    int LeadsWon,
    int LeadsLost,
    decimal WonRevenue,
    int OpenConversations,
    int AwaitingTeam,
    int OpenEscalations,
    int CriticalEscalations,
    int ActiveUsers,
    int PublishedPages,
    int PublishedServices,
    int PublishedProjects,
    int PublishedPosts,
    IReadOnlyList<DailyVolumeDto> LastThirtyDays,
    IReadOnlyList<TopServiceDto> TopServices);

public sealed record DailyVolumeDto(DateOnly Day, int Leads, int ProposalsSent, int Conversations);

public sealed record TopServiceDto(Guid ServiceId, string Name, int Leads);
