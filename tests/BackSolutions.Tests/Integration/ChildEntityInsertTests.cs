using BackSolutions.Core.Common;
using BackSolutions.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BackSolutions.Tests.Integration;

/// <summary>
/// Regresión del bug de los hijos nunca insertados.
///
/// Cuando una entidad se agregaba a la colección de navegación de un padre que ya estaba
/// trackeado, EF no la marcaba como Added (porque la clave Guid ya venía asignada, y EF
/// asume que eso significa que la fila existe) y generaba un UPDATE sobre una fila
/// inexistente. El resultado era un DbUpdateConcurrencyException que el middleware
/// reportaba como un 400 de integridad sin decir por qué.
///
/// Afectaba al alta de mensajes en una conversación existente, al alta de notas internas,
/// a las líneas de una propuesta, imágenes de un proyecto, secciones de una página, tags
/// de un post y a la asignación de roles a un usuario que ya existía.
///
/// Estos tests usan HTTP a propósito: el bug vivía en la combinación servicio +
/// ChangeTracker, y probarlo sobre el servicio en aislamiento se lo Pierdo.
/// </summary>
public sealed class ChildEntityInsertTests : IntegrationTestBase
{
    private TestUser _owner = null!;
    private string _ownerToken = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _owner = await WithScopeAsync(services =>
            TestData.CreateUserAsync(services, "owner@prueba.test", RoleNames.Owner));
        _ownerToken = await Api.LoginAsync(_owner.Email, _owner.Password);
    }

    [Fact]
    public async Task El_equipo_puede_responder_un_mensaje_del_cliente()
    {
        var conversation = await Api.Post("/api/public/conversations", new
        {
            subject = "Consulta",
            message = "Cuánto cuesta?",
            name = "Cliente",
            email = "cliente@prueba.test"
        });

        Assert.True(conversation.Status == 201, $"{conversation.Status} {conversation.Body}");
        var conversationId = conversation.Str("id");

        var reply = await Api.Post(
            $"/api/admin/conversations/{conversationId}/messages",
            new { body = "Hola, te paso el detalle." },
            _ownerToken);

        Assert.True(reply.Status == 201, $"{reply.Status} {reply.Body}");
        Assert.Equal("Hola, te paso el detalle.", reply.Str("body"));
    }

    [Fact]
    public async Task Se_puede_agregar_una_nota_interna_sin_romperse()
    {
        var conversation = await Api.Post("/api/public/conversations", new
        {
            subject = "Consulta",
            message = "Hola",
            name = "Cliente",
            email = "cliente@prueba.test"
        });

        var conversationId = conversation.Str("id");

        var note = await Api.Post(
            $"/api/admin/conversations/{conversationId}/messages",
            new { body = "CUIDADO: margen bajo", isInternal = true },
            _ownerToken);

        Assert.True(note.Status == 201, $"{note.Status} {note.Body}");
        Assert.Equal("True", note.Str("isInternal"));
    }

    [Fact]
    public async Task La_nota_interna_no_le_aparece_al_cliente()
    {
        var conversation = await Api.Post("/api/public/conversations", new
        {
            subject = "Consulta",
            message = "Hola",
            name = "Cliente",
            email = "cliente@prueba.test"
        });

        var conversationId = conversation.Str("id");
        var publicToken = conversation.Str("publicToken");

        await Api.Post(
            $"/api/admin/conversations/{conversationId}/messages",
            new { body = "CUIDADO: margen bajo", isInternal = true },
            _ownerToken);

        var history = await Api.Get($"/api/public/conversations/{publicToken}");

        Assert.True(history.Status == 200, $"{history.Status} {history.Body}");
        Assert.DoesNotContain("CUIDADO", history.Body);
    }

    [Fact]
    public async Task Se_pueden_asignar_roles_a_un_usuario_que_ya_existia()
    {
        var existing = await WithScopeAsync(services =>
            TestData.CreateUserAsync(services, "soporte@prueba.test", RoleNames.Support));

        var assign = await Api.Put(
            $"/api/admin/users/{existing.Id}/roles",
            new { roles = new[] { RoleNames.Support, RoleNames.Provider } },
            _ownerToken);

        Assert.True(assign.Status == 200, $"{assign.Status} {assign.Body}");
    }

    [Fact]
    public async Task Agregar_una_imagen_a_un_proyecto_existente_no_falla()
    {
        var projectId = await CreateProjectAsync();

        var withImages = await Api.Put(
            $"/api/admin/portfolio/{projectId}",
            new
            {
                title = "Proyecto con imagen",
                summary = "Resumen",
                clientName = "Cliente",
                status = "published",
                images = new[]
                {
                    new { url = "https://ejemplo.test/1.jpg", altText = "Primera", sortOrder = 0 },
                    new { url = "https://ejemplo.test/2.jpg", altText = "Segunda", sortOrder = 1 }
                }
            },
            _ownerToken);

        Assert.True(withImages.Status == 200, $"{withImages.Status} {withImages.Body}");
        Assert.Contains("1.jpg", withImages.Body);
        Assert.Contains("2.jpg", withImages.Body);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var created = await Api.Post(
            "/api/admin/portfolio",
            new
            {
                title = "Proyecto de prueba",
                summary = "Resumen inicial",
                clientName = "Cliente",
                status = "published"
            },
            _ownerToken);

        Assert.True(created.Status == 201, $"{created.Status} {created.Body}");

        return Guid.Parse(created.Str("id")!);
    }
}
