using System.Data.Common;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Tests.Infrastructure;

/// <summary>
/// Envuelve cada test de integración en una transacción que se revierte al terminar.
///
/// La aplicación abre sus propias transacciones: Blog, Page, Portfolio y Proposal lo
/// hacen porque necesitan que borrar hijos huérfanos y crear el registro sea atómico.
/// Con una transacción ya abierta en la conexión, SqlClient lanza "does not support
/// parallel transactions" y el test revienta antes de llegar al código que quiere
/// probar. Por eso los servicios no abren la suya: pasan por
/// <c>ITransactionRunner</c>, que reutiliza la transacción que ya encuentra abierta.
///
/// Por eso esta clase no intercepta nada, pese al nombre que tuvo antes. Solo:
///
///  1. abre la transacción del test sobre la conexión compartida;
///  2. enlista cada <see cref="AppDbContext"/> que la aplicación crea, porque los
///     contextos son nuevos en cada request y hay que meterlos a mano;
///  3. la revierte cuando el test termina.
///
/// Limitación conocida: si un servicio hiciera rollback explícito de su transacción, la
/// del test se revertiría con ella. Con <c>ITransactionRunner</c> ya no hay rollback
/// por debajo, pero queda anotado en la deuda técnica por si alguien reintroduce uno.
/// </summary>
internal sealed class TestTransaction
{
    private DbTransaction? _ambient;

    /// <summary>La transacción que el test revierte al terminar.</summary>
    internal DbTransaction? Ambient => _ambient;

    /// <summary>
    /// Enlista en la transacción del test un contexto recién creado.
    /// </summary>
    internal void Enlist(DbContext context)
    {
        if (_ambient is not null)
        {
            context.Database.UseTransaction(_ambient);
        }
    }

    /// <summary>
    /// Abre la transacción directamente sobre la conexión.
    ///
    /// No se abre a través de un AppDbContext descartable porque disposear el contexto
    /// dispone también su transacción, y la que queda en manos de esta clase sale
    /// cerrada: el fallo se ve recién cuando un contexto pide UseTransaction y EF
    /// responde "the specified transaction is not associated with the current connection".
    /// </summary>
    internal async Task BeginAsync(DbConnection connection, CancellationToken cancellationToken = default)
        => _ambient = await connection.BeginTransactionAsync(cancellationToken);

    /// <summary>Revierte y libera la transacción del test.</summary>
    internal async Task ResetAsync()
    {
        if (_ambient is null)
        {
            return;
        }

        try
        {
            await _ambient.RollbackAsync();
        }
        catch (InvalidOperationException)
        {
            // Si algo la revirtió antes, no hay nada que deshacer.
        }

        await _ambient.DisposeAsync();
        _ambient = null;
    }
}
