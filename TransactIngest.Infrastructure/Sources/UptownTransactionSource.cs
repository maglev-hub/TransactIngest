using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Interfaces;
using TransactIngest.Application.Models;

namespace TransactIngest.Infrastructure.Sources
{
    public sealed class UptownTransactionSource : InMemoryTransactionSource
    {
        public UptownTransactionSource(
            TimeProvider timeProvider,
            IOptionsMonitor<MockSourceOptions> options)
            : base(SourceSystems.Uptown, options, CreateTransactions(timeProvider.GetUtcNow().UtcDateTime))
        {
        }

        private static SourceTransaction[] CreateTransactions(DateTime anchorUtc) =>
            new[]
            {
            new SourceTransaction
            {
                TransactionId = 100001,
                CardNumber = "***1111",
                LocationCode = "UPTOWN",
                ProductName = "Wireless Mouse",
                Amount = 24.99m,
                TransactionTime = anchorUtc.AddHours(-3)
            },
            new SourceTransaction
            {
                TransactionId = 100002,
                CardNumber = "***2222",
                LocationCode = "UPTOWN",
                ProductName = "27-inch Monitor",
                Amount = 199.99m,
                TransactionTime = anchorUtc.AddHours(-6)
            },
            new SourceTransaction
            {
                TransactionId = 100003,
                CardNumber = "***3333",
                LocationCode = "UPTOWN",
                ProductName = "HD Webcam",
                Amount = 59.95m,
                TransactionTime = anchorUtc.AddHours(-8)
            }
            };
    }
}
