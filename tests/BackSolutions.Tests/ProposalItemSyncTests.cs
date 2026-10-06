using BackSolutions.Core.Dtos.Proposals;
using BackSolutions.Core.Entities.Leads;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Tests;

/// <summary>
/// Reproduce el 500 al editar una propuesta.
///
/// El servicio hacía <c>Items.Clear()</c> y volvía a agregar las líneas con el Id que manda el
/// cliente. <c>Clear()</c> solo las saca de la colección: las entidades siguen trackeadas, así que
/// EF rechazaba la segunda instancia con la misma clave y la actualización moría en un
/// <c>InvalidOperationException</c> sin detalle para el usuario.
///
/// El test usa el provider InMemory a propósito: el conflicto es del ChangeTracker, no del
/// proveedor, y así no hace falta tocar la base compartida.
/// </summary>
public sealed class ProposalItemSyncTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static (AppDbContext Db, Proposal Proposal) SeedProposal()
    {
        var db = NewContext();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            Number = "PRO-TEST",
            Title = "Original",
            SummaryHtml = "Resumen",
            Currency = "ARS",
            Status = Core.Enums.ProposalStatus.Draft
        };

        proposal.Items.Add(new ProposalItem
        {
            Id = Guid.NewGuid(),
            Proposal = proposal,
            Description = "Landing",
            Quantity = 1m,
            UnitPrice = 100m,
            SortOrder = 0
        });

        db.Proposals.Add(proposal);
        db.SaveChanges();

        db.ChangeTracker.Clear();

        return (db, proposal);
    }

    /// <summary>
    /// El patrón viejo: <c>Clear()</c> + <c>AddNew</c> con el Id existente. Se mantiene el test
    /// para documentar por qué no se puede volver a usar.
    /// </summary>
    [Fact]
    public void Clear_y_reagregar_el_mismo_Id_falla_con_doble_seguimiento()
    {
        var (db, _) = SeedProposal();

        var tracked = db.Proposals.Include(p => p.Items).First();
        var itemId = tracked.Items.Single().Id;

        var request = new SaveProposalItemRequest(itemId, "Landing", 1m, 100m, null, 0);

        var exception = Record.Exception(() =>
        {
            tracked.Items.Clear();

            var replacement = new ProposalItem
            {
                Id = itemId,
                ProposalId = tracked.Id,
                Description = request.Description
            };

            tracked.Items.Add(replacement);
            db.Entry(replacement).State = EntityState.Added;
            db.SaveChanges();
        });

        Assert.NotNull(exception);
        Assert.Contains("already being tracked", exception.Message, StringComparison.OrdinalIgnoreCase);

        db.Dispose();
    }

    /// <summary>
    /// Lo que hace el servicio hoy: reutilizar la entidad ya trackeada en vez de crear otra con la
    /// misma clave.
    /// </summary>
    [Fact]
    public void sincronizar_en_el_lugar_conserva_la_entidad_y_actualiza_los_valores()
    {
        var (db, _) = SeedProposal();

        var proposal = db.Proposals.Include(p => p.Items).First();
        var originalId = proposal.Items.Single().Id;

        var items = proposal.Items.ToDictionary(i => i.Id);

        // Equivalente a SyncItems: se busca el Id recibido entre las líneas ya trackeadas.
        items.Remove(originalId, out var reused);
        Assert.NotNull(reused);

        reused.Description = "Landing editada";
        reused.UnitPrice = 250m;
        reused.SortOrder = 0;

        db.SaveChanges();
        db.ChangeTracker.Clear();

        var reloaded = db.Proposals.Include(p => p.Items).Single();
        Assert.Equal(originalId, reloaded.Items.Single().Id);
        Assert.Equal("Landing editada", reloaded.Items.Single().Description);
        Assert.Equal(250m, reloaded.Items.Single().UnitPrice);

        db.Dispose();
    }

    /// <summary>
    /// Una línea que vuelve se actualiza conservando su Id, una que llega con Id desconocido se
    /// agrega, y una que no vuelve se elimina. Reproduce el caso real de editar una propuesta con
    /// una línea borrada.
    /// </summary>
    [Fact]
    public void la_que_vuelve_se_actualiza_la_nueva_se_agrega_y_la_que_no_vuelve_se_elimina()
    {
        var (db, _) = SeedProposal();

        // Segunda línea, que el cliente va a quitar.
        var droppedId = Guid.NewGuid();
        var proposalId = db.Proposals.Select(p => p.Id).Single();
        db.ProposalItems.Add(new ProposalItem
        {
            Id = droppedId,
            ProposalId = proposalId,
            Description = "Se va",
            UnitPrice = 5m,
            SortOrder = 1
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var proposal = db.Proposals.Include(p => p.Items).Single();
        Assert.Equal(2, proposal.Items.Count);

        var keptId = proposal.Items.Single(i => i.Description == "Landing").Id;
        var addedId = Guid.NewGuid();

        // SyncItems: se consumen del diccionario las líneas que vuelven.
        var byId = proposal.Items.ToDictionary(i => i.Id);
        byId.Remove(keptId, out var reused);
        reused!.Description = "Landing editada";
        reused.UnitPrice = 250m;
        reused.SortOrder = 0;

        var fresh = new ProposalItem { Id = addedId, ProposalId = proposal.Id, Description = "Nueva", UnitPrice = 10m };
        proposal.Items.Add(fresh);
        db.Entry(fresh).State = EntityState.Added;

        foreach (var removed in byId.Values)
        {
            proposal.Items.Remove(removed);
        }

        db.SaveChanges();
        db.ChangeTracker.Clear();

        var reloaded = db.Proposals.Include(p => p.Items).Single();
        Assert.Equal(2, reloaded.Items.Count);
        Assert.DoesNotContain(reloaded.Items, i => i.Id == droppedId);
        Assert.Contains(reloaded.Items, i => i.Id == keptId && i.Description == "Landing editada" && i.UnitPrice == 250m);
        Assert.Contains(reloaded.Items, i => i.Id == addedId);

        db.Dispose();
    }
}