using BackSolutions.Core.Dtos.Chat;
using Microsoft.AspNetCore.SignalR;

namespace BackSolutions.Api.Hubs;

/// <summary>
/// Reemite al Hub lo que publica <see cref="ChatEventBus"/>.
///
/// Es el puente entre el servicio (scoped) y el transporte (singleton), y por eso corre
/// como hosted service.
///
/// El punto delicado es no filtrar notas internas. Los mensajes del equipo con
/// <c>IsInternal</c> van al grupo del equipo y jamás al del cliente: si se mandaran al
/// grupo de la conversación, la nota sería pública para cualquiera que tenga el token de
/// ese hilo, que es justo lo que el filtro del historial evita.
/// </summary>
public sealed class ChatHubBroadcaster : BackgroundService
{
    private readonly ChatEventBus _bus;
    private readonly IHubContext<ChatHub> _teamHub;
    private readonly IHubContext<ConversationHub> _conversationHub;

    public ChatHubBroadcaster(
        ChatEventBus bus,
        IHubContext<ChatHub> teamHub,
        IHubContext<ConversationHub> conversationHub)
    {
        _bus = bus;
        _teamHub = teamHub;
        _conversationHub = conversationHub;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _bus.MessageAdded += OnMessageAddedAsync;
        _bus.EscalationChanged += OnEscalationChangedAsync;

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        // Sin esto, cada reciclo del host dejaría un suscriptor apuntando a un hub que ya
        // no existe, y cada mensaje se intentaría enviar N veces.
        _bus.MessageAdded -= OnMessageAddedAsync;
        _bus.EscalationChanged -= OnEscalationChangedAsync;

        base.Dispose();
    }

    private async Task OnMessageAddedAsync(MessageDto message, Guid conversationId, CancellationToken cancellationToken)
    {
        // El equipo ve todos los mensajes, internos incluidos.
        await _teamHub.Clients
            .Group(ChatHub.TeamGroup)
            .SendAsync("messageAdded", message, cancellationToken);

        if (message.IsInternal)
        {
            // Nota interna: el cliente no se entera ni de que existe.
            return;
        }

        await _conversationHub.Clients
            .Group(ConversationHub.GroupFor(conversationId))
            .SendAsync("messageAdded", message, cancellationToken);
    }

    private async Task OnEscalationChangedAsync(EscalationDto escalation, Guid conversationId, CancellationToken cancellationToken)
    {
        // El escalamiento es un asunto interno: lo ve el equipo, no el cliente.
        await _teamHub.Clients
            .Group(ChatHub.TeamGroup)
            .SendAsync("escalationChanged", escalation, cancellationToken);
    }
}
