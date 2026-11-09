using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TransactIngest.Application.Interfaces;
using TransactIngest.Domain.Entities;
using TransactIngest.Infrastructure.Data;

namespace TransactIngest.Infrastructure.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly AppDbContext _context;

        public TransactionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyCollection<TransactionRecord>>
            GetBySourceSystemAsync(
                string sourceSystem,
                CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Where(transaction =>
                    transaction.SourceSystem == sourceSystem)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyCollection<TransactionRecord>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AsNoTracking()
                .OrderByDescending(transaction =>
                    transaction.TransactionTime)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            TransactionRecord transaction,
            CancellationToken cancellationToken = default)
        {
            await _context.Transactions.AddAsync(
                transaction,
                cancellationToken);
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
