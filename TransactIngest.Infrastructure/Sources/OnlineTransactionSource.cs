using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Interfaces;
using TransactIngest.Application.Models;

namespace TransactIngest.Infrastructure.Sources
{
    public sealed class OnlineTransactionSource : InMemoryTransactionSource
    {
        public OnlineTransactionSource(
            TimeProvider timeProvider,
            IOptionsMonitor<MockSourceOptions> options)
            : base(SourceSystems.Online, options, CreateTransactions(timeProvider.GetUtcNow().UtcDateTime))
        {
        }

        private static SourceTransaction[] CreateTransactions(DateTime anchorUtc) =>
            new[]
            {
            new SourceTransaction
            {
                TransactionId = 100001,
                CardNumber = "***4444",
                LocationCode = "ONLINE",
                ProductName = "USB-C Docking Station",
                Amount = 129.95m,
                TransactionTime = anchorUtc.AddHours(-2)
            }
            };
    }
}
