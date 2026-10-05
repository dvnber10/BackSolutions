using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Admin;

namespace BackSolutions.Core.Interfaces;

/// <summary>Alta, edición y roles del equipo.</summary>
public interface IUserService
{
    Task<PagedResult<UserListItemDto>> ListAsync(int? page, int? pageSize, string? search, bool? activeOnly, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default);

    Task<UserListItemDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserListItemDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserListItemDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza el conjunto de roles del usuario. Siempre deja al menos uno.</summary>
    Task<UserListItemDto> AssignRolesAsync(Guid id, AssignRolesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Resetea el bloqueo por intentos fallidos. Lo usa el Owner desde el panel.</summary>
    Task UnlockAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cambio de contraseña hecho por otro usuario (Owner/Admin), no por el propio usuario.</summary>
    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Da de baja un usuario. No borra la fila: la activa en false para conservar el historial.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Métricas para la pantalla inicial del panel.</summary>
public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
