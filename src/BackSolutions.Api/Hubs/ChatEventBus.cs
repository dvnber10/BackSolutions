using BackSolutions.Core.Dtos.Chat;

namespace BackSolutions.Api.Hubs;

/// <summary>
/// Bus de eventos del chat, singleton.
///
/// Existe por una razón concreta: <c>IChatService</c> es scoped, o sea que hay una
/// instancia distinta por petición HTTP. Su evento <c>MessageAdded</c> lo dispara la
/// instancia que atendió el POST, mientras que el Hub se resuelve en otra instancia
/// completamente distinta. Un hosted service que se suscribiera al evento se anotaría a la
/// instancia equivocada y ninguna notificación llegaría nunca al cliente.
///
/// El servicio publica acá y este bus, que sí es único, reparte a los hubs. Así el
/// servicio sigue sin saber qué es SignalR y el transporte no necesita conocer el
/// servicio.
/// </summary>
public sealed class ChatEventBus
{
    /// <summary>Un mensaje nuevo. El Guid es el id de la conversación.</summary>
    public event Func<MessageDto, Guid, CancellationToken, Task>? MessageAdded;

    /// <summary>Cambió el estado de un escalamiento.</summary>
    public event Func<EscalationDto, Guid, CancellationToken, Task>? EscalationChanged;

    /// <summary>
    /// Publica un mensaje nuevo a todos los suscriptores.
    ///
    /// Notificar en tiempo real es un efecto secundario: si el Hub está caído o un
    /// cliente se desconectó a mitad del envío, el mensaje igual tiene que estar guardado
    /// en la base. Por eso un fallo acá se loguea y no se propaga.
    /// </summary>
    public Task PublishMessageAsync(MessageDto message, Guid conversationId, CancellationToken cancellationToken = default) =>
        DispatchAsync(() => MessageAdded, message, conversationId, cancellationToken);

    /// <summary>Publica un cambio de escalamiento. Igual que el mensaje, sin propagar fallos.</summary>
    public Task PublishEscalationAsync(EscalationDto escalation, Guid conversationId, CancellationToken cancellationToken = default) =>
        DispatchAsync(() => EscalationChanged, escalation, conversationId, cancellationToken);

    private async Task DispatchAsync<TPayload>(
        Func<Func<TPayload, Guid, CancellationToken, Task>?> selector,
        TPayload payload,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var handlers = selector();

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                await ((Func<TPayload, Guid, CancellationToken, Task>)handler)(payload, conversationId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // El cliente se fue. No es un error.
            }
            catch (Exception)
            {
                // Se traga a propósito: una notificación fallida no puede tumbar el POST
                // que guardó el mensaje. El cliente recupera lo que se perdió recargando
                // el historial por HTTP.
            }
        }
    }
}
