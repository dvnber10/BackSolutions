using BackSolutions.Api.Security;
using BackSolutions.Core.Dtos.Chat;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BackSolutions.Api.Hubs;

/// <summary>
/// Chat en tiempo real del equipo.
///
/// Entra con JWT y ve toda la bandeja. El cliente anónimo usa
/// <see cref="ConversationHub"/>, que es una clase aparte a propósito: si ambos
/// compartieran un Hub con un único grupo, bastaría adivinar el nombre del grupo para
/// leer las notas internas del equipo.
///
/// Este Hub no escribe nada. Los mensajes se mandan por los endpoints HTTP, que son los
/// que validan, aplican límites de peticiones y dejan registro. Si el Hub aceptara
/// escrituras habría dos caminos distintos hacia el mismo servicio, y el servicio dejaría
/// de ser el único lugar donde se decide qué puede ver cada uno.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.TeamMember)]
public sealed class ChatHub : Hub
{
    /// <summary>Grupo del equipo. Se une solo al conectar.</summary>
    public const string TeamGroup = "team";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup);

        await Clients.Caller.SendAsync("connected", new
        {
            userId = Context.UserIdentifier,
            group = TeamGroup
        });

        await base.OnConnectedAsync();
    }
}

/// <summary>
/// Chat en tiempo real del cliente web.
///
/// El visitante no tiene JWT: se identifica con el token de su conversación, que es un
/// secreto distinto al mecanismo de sesión del equipo. Si el token falta o no corresponde
/// a ninguna conversación, la conexión se aborta en vez de quedar viva sin grupo.
///
/// El grupo es por id de conversación, no por token. El nombre del grupo lo elige siempre
/// el servidor, y solo después de comprobar que el token es de esa conversación: un
/// cliente no puede pedir entrar en un grupo arbitrario, así que usar el id no abre una
/// vía para colarse en el hilo de otro. Y a diferencia de un grupo por token, este
/// reparte bien cuando dos pestañas están abiertas en la misma conversación.
/// </summary>
[AllowAnonymous]
public sealed class ConversationHub : Hub
{
    private readonly IChatService _chat;

    public ConversationHub(IChatService chat) => _chat = chat;

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        if (httpContext is null)
        {
            Context.Abort();
            return;
        }

        var publicToken = httpContext.Request.Query["publicToken"].ToString();

        if (string.IsNullOrWhiteSpace(publicToken))
        {
            Context.Abort();
            return;
        }

        var conversation = await _chat.GetPublicConversationAsync(publicToken, Context.ConnectionAborted);

        if (conversation is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(conversation.Id));

        await Clients.Caller.SendAsync("connected", new
        {
            conversationId = conversation.Id,
            status = conversation.Status.ToString()
        });

        await base.OnConnectedAsync();
    }

    /// <summary>Grupo de una conversación. Solo el servidor decide a cuál se entra.</summary>
    public static string GroupFor(Guid conversationId) => $"conversation:{conversationId}";
}
