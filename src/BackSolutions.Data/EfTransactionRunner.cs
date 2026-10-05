using BackSolutions.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Data;

/// <inheritdoc />
public sealed class EfTransactionRunner(AppDbContext db) : ITransactionRunner
{
    public async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Ya hay una transacción en curso: se participa de ella en vez de abrir otra.
        //
        // En producción esto no se dispara nunca, porque nadie abre una transacción
        // antes que el servicio. En los tests de integración sí, siempre: el harness
        // envuelve cada test en una transacción que revierte al terminar, y abrir una
        // segunda acá pelearía con EF Core ("the connection is already in a
        // transaction") y con SqlClient ("does not support parallel transactions").
        if (db.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var result = await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
