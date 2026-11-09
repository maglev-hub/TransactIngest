using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Models;

namespace TransactIngest.Application.Interfaces
{
    public interface ITransactionSource
    {
        string SourceSystem { get; }

        Task<IReadOnlyCollection<SourceTransaction>> GetTransactionsAsync(
            CancellationToken cancellationToken = default);
    }
}
