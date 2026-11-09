using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Domain.Entities;

namespace TransactIngest.Application.Interfaces
{
    public interface ITransactionRepository
    {
        //loads existing transactions from one source for reconciliation.
        Task<IReadOnlyCollection<TransactionRecord>> GetBySourceSystemAsync(
            string sourceSystem,
            CancellationToken cancellationToken = default);

        //Get transactions to the API and frontend.
        Task<IReadOnlyCollection<TransactionRecord>> GetAllAsync(
            CancellationToken cancellationToken = default);

        //inserts a new transaction record.
        Task AddAsync(
            TransactionRecord transaction,
            CancellationToken cancellationToken = default);

        //saves new transactions, updates, revocations, and audit records
        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}
