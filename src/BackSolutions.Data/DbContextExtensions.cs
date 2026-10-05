using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Data;

public static class DbContextExtensions
{
    /// <summary>
    /// Marca una entidad como nueva de forma explícita y devuelve la misma instancia.
    ///
    /// Hace falta porque las claves Guid las asigna el código y no la base. EF Core usa
    /// el valor de la clave para adivinar el estado de una entidad sin trackear: si ya
    /// viene asignada asume que la fila existe y la deja en <see cref="EntityState.Modified"/>.
    /// Al agregarla entonces a la colección de navegación de un padre que ya está
    /// trackeado, EF genera un UPDATE en lugar de un INSERT y la operación falla con
    /// DbUpdateConcurrencyException ("expected to affect 1 row(s), but actually affected 0"),
    /// que además se reporta al cliente como un 400 de integridad.
    ///
    /// <see cref="DbSet{TEntity}.Add(TEntity)"/> esquiva el problema porque fija el estado
    /// a mano, pero las colecciones de navegación no lo hacen. Por eso los hijos nuevos se
    /// agregan con este método en vez de con <c>parent.Children.Add(...)</c>.
    /// </summary>
    public static TEntity AddNew<TEntity>(this DbContext context, TEntity entity)
        where TEntity : class
    {
        context.Entry(entity).State = EntityState.Added;

        return entity;
    }
}
