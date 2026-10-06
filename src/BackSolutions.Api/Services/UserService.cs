using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Admin;
using BackSolutions.Core.Entities.Identity;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BackSolutions.Api.Services;

/// <summary>
/// Gestión de cuentas del equipo.
///
/// Dos invariantes que sostienen el resto:
///
/// - No se borran usuarios, se desactivan (User.IsActive). Los leads, propuestas y
///   mensajes quedan atribuidos a alguien real; borrar la fila dejarían responsables
///   huérfanos y rompería la trazabilidad comercial.
///
/// - El último Owner no se puede desactivar ni degradar. Sin Owner el sistema queda
///   sin quien pueda administrar usuarios, y de ahí no se sale por la UI.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext db,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IClock clock,
        IEmailSender emailSender,
        ILogger<UserService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _clock = clock;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<PagedResult<UserListItemDto>> ListAsync(
        int? page,
        int? pageSize,
        string? search,
        bool? activeOnly,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (activeOnly == true)
        {
            query = query.Where(u => u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var like = $"%{term}%";
            query = query.Where(u => EF.Functions.Like(u.Email, like) || EF.Functions.Like(u.FullName, like));
        }

        var total = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var ids = users.Select(u => u.Id).ToList();

        var openConversations = await _db.Conversations
            .Where(c => ids.Contains(c.AssignedToUserId!.Value) && c.Status != Core.Enums.ConversationStatus.Closed)
            .GroupBy(c => c.AssignedToUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var assignedLeads = await _db.Leads
            .Where(l => ids.Contains(l.HandledByUserId!.Value))
            .GroupBy(l => l.HandledByUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var now = _clock.UtcNow;

        var items = users.Select(u => new UserListItemDto(
            u.Id,
            u.Email,
            u.FullName,
            u.AvatarUrl,
            u.IsActive,
            u.MustChangePassword,
            u.LockoutEndsAtUtc > now,
            u.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
            openConversations.TryGetValue(u.Id, out var conversations) ? conversations : 0,
            assignedLeads.TryGetValue(u.Id, out var leads) ? leads : 0,
            u.LastLoginAtUtc,
            u.CreatedAtUtc)).ToList();

        return PagedResult<UserListItemDto>.Create(items, total, currentPage, size);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default) =>
        await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.IsSystem, r.UserRoles.Count))
            .ToListAsync(cancellationToken);

    public async Task<UserListItemDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        var now = _clock.UtcNow;

        return new UserListItemDto(
            user.Id,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            user.IsActive,
            user.MustChangePassword,
            user.LockoutEndsAtUtc > now,
            user.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
            await _db.Conversations.CountAsync(c => c.AssignedToUserId == user.Id && c.Status != Core.Enums.ConversationStatus.Closed, cancellationToken),
            await _db.Leads.CountAsync(l => l.HandledByUserId == user.Id, cancellationToken),
            user.LastLoginAtUtc,
            user.CreatedAtUtc);
    }

    public async Task<UserListItemDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = AuthService.NormalizeEmail(request.Email);

        if (email.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = ["El email es obligatorio."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.FullName)] = ["El nombre es obligatorio."]
            });
        }

        AuthService.ValidatePasswordStrength(request.Password);

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("Ya existe un usuario con ese email.");
        }

        var roles = await ResolveRolesAsync(request.Roles ?? [RoleNames.Support], cancellationToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Phone = request.Phone?.Trim(),
            AvatarUrl = request.AvatarUrl?.Trim(),
            IsActive = true,
            // Por defecto se exige cambiar la clave: la que eligió el Owner es provisional.
            MustChangePassword = request.MustChangePassword,
            CreatedAtUtc = _clock.UtcNow
        };

        _db.Users.Add(user);

        foreach (var role in roles)
        {
            var link = new UserRole { UserId = user.Id, RoleId = role.Id };
            user.UserRoles.Add(link);
            _db.AddNew(link);
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailSender.SendEmailAsync(
                user.Email,
                "Bienvenido a BackSolutions - Tus credenciales de acceso",
                $@"
                <p>Hola <strong>{user.FullName}</strong>,</p>
                <p>Se ha creado tu cuenta en el panel de BackSolutions.</p>
                <p>Tu contraseña temporal es: <strong>{request.Password}</strong></p>
                <p>Te recomendamos cambiarla al iniciar sesión por primera vez.</p>
                <p>Atentamente,<br/><strong>Equipo BackSolutions</strong></p>",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el email de bienvenida al usuario {Email}", user.Email);
        }

        return await GetByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserListItemDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (request.FullName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.FullName)] = ["El nombre no puede quedar vacío."]
                });
            }

            user.FullName = request.FullName.Trim();
        }

        if (request.Phone is not null)
        {
            user.Phone = request.Phone.Trim();
        }

        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = request.AvatarUrl.Trim();
        }

        if (request.IsActive is { } isActive && isActive != user.IsActive)
        {
            if (!isActive)
            {
                await GuardLastOwnerAsync(id, cancellationToken);
            }

            user.IsActive = isActive;

            // Desactivar corta las sesiones abiertas: si no, el token sigue sirviendo
            // hasta que expire.
            if (!isActive)
            {
                await _tokens.RevokeAllForUserAsync(id, cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserListItemDto> AssignRolesAsync(Guid id, AssignRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (request.Roles.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Roles)] = ["El usuario debe tener al menos un rol."]
            });
        }

        var roles = await ResolveRolesAsync(request.Roles, cancellationToken);
        var wantsOwner = roles.Any(r => r.Name == RoleNames.Owner);

        var hadOwner = user.UserRoles.Any(ur => ur.Role.Name == RoleNames.Owner);

        if (hadOwner && !wantsOwner)
        {
            await GuardLastOwnerAsync(id, cancellationToken);
        }

        var wantedRoleIds = roles.Select(role => role.Id).ToHashSet();
        var assignedRoleIds = user.UserRoles.Select(link => link.RoleId).ToHashSet();

        foreach (var link in user.UserRoles.Where(link => !wantedRoleIds.Contains(link.RoleId)).ToList())
        {
            user.UserRoles.Remove(link);
            _db.Entry(link).State = EntityState.Deleted;
        }

        foreach (var role in roles.Where(role => !assignedRoleIds.Contains(role.Id)))
        {
            var link = new UserRole { UserId = user.Id, RoleId = role.Id };
            user.UserRoles.Add(link);
            _db.AddNew(link);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _tokens.RevokeAllForUserAsync(id, cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task UnlockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        user.FailedLoginAttempts = 0;
        user.LockoutEndsAtUtc = null;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        AuthService.ValidatePasswordStrength(request.NewPassword);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = request.MustChangePassword;
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAtUtc = null;

        await _db.SaveChangesAsync(cancellationToken);

        await _tokens.RevokeAllForUserAsync(id, cancellationToken);

        try
        {
            await _emailSender.SendEmailAsync(
                user.Email,
                "Restablecimiento de contraseña - BackSolutions",
                $@"
                <p>Hola <strong>{user.FullName}</strong>,</p>
                <p>Se ha restablecido tu contraseña en el panel de BackSolutions.</p>
                <p>Tu nueva contraseña temporal es: <strong>{request.NewPassword}</strong></p>
                <p>Atentamente,<br/><strong>Equipo BackSolutions</strong></p>",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el email de restablecimiento al usuario {Email}", user.Email);
        }
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (id == _currentUser.UserId)
        {
            throw new ConflictException("No podés desactivar tu propio usuario.");
        }

        await GuardLastOwnerAsync(id, cancellationToken);

        user.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        await _tokens.RevokeAllForUserAsync(id, cancellationToken);
    }

    private async Task GuardLastOwnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isOwner = await _db.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.Role!.Name == RoleNames.Owner, cancellationToken);

        if (!isOwner)
        {
            return;
        }

        var ownerCount = await _db.UserRoles
            .CountAsync(ur => ur.Role!.Name == RoleNames.Owner, cancellationToken);

        if (ownerCount <= 1)
        {
            throw new ConflictException(
                "Es el único Owner del sistema. Asigná otro Owner antes de quitarle el rol o desactivar la cuenta.");
        }
    }

    private async Task<IReadOnlyList<Role>> ResolveRolesAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        var requested = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["roles"] = ["Se debe indicar al menos un rol."]
            });
        }

        var roles = await _db.Roles
            .Where(r => requested.Contains(r.Name))
            .ToListAsync(cancellationToken);

        var unknown = requested
            .Where(n => roles.All(r => !string.Equals(r.Name, n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (unknown.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["roles"] = [$"Roles desconocidos: {string.Join(", ", unknown)}"]
            });
        }

        return roles;
    }
}
