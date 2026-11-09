using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Interfaces;
using TransactIngest.Application.Models;

namespace TransactIngest.Infrastructure.Sources
{
    public sealed class DowntownTransactionSource : InMemoryTransactionSource
    {
        public DowntownTransactionSource(
            TimeProvider timeProvider,
            IOptionsMonitor<MockSourceOptions> options)
            : base(SourceSystems.Downtown, options, CreateTransactions(timeProvider.GetUtcNow().UtcDateTime))
        {
        }

        private static SourceTransaction[] CreateTransactions(DateTime anchorUtc) =>
            new[]
            {
            new SourceTransaction
            {
                TransactionId = 100001,
                CardNumber = "***0002",
                LocationCode = "DOWNTOWN",
                ProductName = "Mechanical Keyboard",
                Amount = 89.99m,
                TransactionTime = anchorUtc.AddHours(-4)
            },
            new SourceTransaction
            {
                TransactionId = 100006,
                CardNumber = "***6666",
                LocationCode = "DOWNTOWN",
                ProductName = "Noise-Canceling Headphones",
                Amount = 149.99m,
                TransactionTime = anchorUtc.AddHours(-25)
            }
            };
    }

}
