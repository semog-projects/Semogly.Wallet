using Marten;
using Marten.Services;
using IUnitOfWork = Semogly.Wallet.Ledger.Application.Abstractions.IUnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Semogly.Wallet.Ledger.Infrastructure.Persistence;

/// <summary>
/// Une o event store (Marten) e o outbox/inbox (EF Core) em uma única transação do Postgres:
/// eventos do livro-razão, mensagens a publicar e mensagens consumidas são confirmados juntos.
/// </summary>
internal sealed class LedgerUnitOfWork(LedgerDbContext db, IDocumentStore store) : IUnitOfWork, IAsyncDisposable
{
    private IDocumentSession? _session;
    private IDbContextTransaction? _ownedTransaction;

    /// <summary>Sessão do Marten que participa da transação do <see cref="LedgerDbContext"/>.</summary>
    public async Task<IDocumentSession> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return _session;
        }

        // Em consumers, o outbox do MassTransit já abriu a transação; em requisições HTTP, abrimos aqui
        var transaction = db.Database.CurrentTransaction
            ?? (_ownedTransaction = await db.Database.BeginTransactionAsync(cancellationToken));

        _session = store.LightweightSession(
            SessionOptions.ForTransaction((NpgsqlTransaction)transaction.GetDbTransaction()));

        return _session;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Com transação externa, o Marten grava mas não faz commit: quem confirma é o dono da transação
        if (_session is not null)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (_ownedTransaction is not null)
        {
            await _ownedTransaction.CommitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
        }

        if (_ownedTransaction is not null)
        {
            await _ownedTransaction.DisposeAsync();
        }
    }
}
