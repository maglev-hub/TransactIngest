using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TransactIngest.Application.Interfaces;
using TransactIngest.Application.Models;

namespace TransactIngest.Infrastructure.Sources;

public abstract class InMemoryTransactionSource : ITransactionSource
{
    private readonly IReadOnlyCollection<SourceTransaction> _transactions;
    private readonly IOptionsMonitor<MockSourceOptions> _options;

    protected InMemoryTransactionSource(
        string sourceSystem,
        IOptionsMonitor<MockSourceOptions> options,
        IReadOnlyCollection<SourceTransaction> transactions)
    {
        SourceSystem = sourceSystem;
        _options = options;
        _transactions = transactions;
    }

    public string SourceSystem { get; }

    public Task<IReadOnlyCollection<SourceTransaction>> GetTransactionsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = _options.Get(SourceSystem);
        var excludedIds = new HashSet<int>(options.ExcludedTransactionIds);
        var snapshot = new List<SourceTransaction>(_transactions.Count);

        foreach (var transaction in _transactions)
        {
            if (excludedIds.Contains(transaction.TransactionId))
            {
                continue;
            }

            var id = transaction.TransactionId.ToString(CultureInfo.InvariantCulture);
            snapshot.Add(new SourceTransaction
            {
                TransactionId = transaction.TransactionId,
                CardNumber = transaction.CardNumber,
                LocationCode = transaction.LocationCode,
                ProductName = transaction.ProductName,
                Amount = options.AmountOverrides.TryGetValue(id, out var amount)
                    ? amount
                    : transaction.Amount,
                TransactionTime = transaction.TransactionTime
            });
        }

        return Task.FromResult<IReadOnlyCollection<SourceTransaction>>(snapshot);
    }

    public sealed class MockSourceOptions
    {
        public int[] ExcludedTransactionIds { get; set; } = Array.Empty<int>();
        public Dictionary<string, decimal> AmountOverrides { get; set; } = new();
    }

    public static class SourceSystems
    {
        public const string Downtown = "Store Downtown";
        public const string Online = "Online";
        public const string Uptown = "Store Uptown";
    }
}
