namespace BackSolutions.Core.Common;

/// <summary>
/// Ejecuta una unidad de trabajo dentro de una transacción.
///
/// Existe por dos motivos que parecían el mismo pero no lo son. En producción, un
/// servicio que hace varios SaveChanges necesita que todos o ninguno se appliquen:
/// sin esto, guardar la cabecera de una propuesta y fallar al guardar las líneas
/// deja basura. Y en los tests de integración, el harnessReversible abre una
/// transacción por test y la revierte al terminar; si el servicio abre la suya
/// encima, EF Core y SqlClient lo rechazan y no hay forma de interceptarlo desde
/// una conexión provista por el usuario.
///
/// Por eso la regla es: si ya hay una transacción en curso, se usa esa. En
/// producción nunca hay una, así que el comportamiento es el de siempre. En tests
/// siempre hay una, así que el servicio se vuelve un passthrough y el rollback del
/// test se lleva todo.
///
/// Ojo: por esto <see cref="RunAsync{T}"/> no es atómico por sí solo. La unidad de
/// trabajo completa debe caber adentro del callback, incluso el Commit.
/// </summary>
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
