using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Interfaces;
using TransactIngest.Application.Models;
using TransactIngest.Application.Services;
using TransactIngest.Domain.Entities;
using TransactIngest.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace TransactIngest.Tests;

public class ImportServiceTests
{
    private static readonly DateTimeOffset TestNow =
    new(
        2026, 10, 15,
        12, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public async Task ImportAsync_NewTransaction_AddsTransaction()
    {
        // Arrange
        var sourceTransaction = new SourceTransaction
        {
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = TestNow.UtcDateTime
        };

        var source = new FakeTransactionSource(
            "Test Source",
            new[] { sourceTransaction });

        var repository = new FakeTransactionRepository();

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            new FixedTimeProvider(TestNow));

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(1, result.NewTransactions);
        Assert.Equal(0, result.UpdatedTransactions);
        Assert.Equal(0, result.RevokedTransactions);
        Assert.Equal(1, result.SuccessfulSources);
        Assert.Equal(0, result.FailedSources);

        var savedTransaction = Assert.Single(repository.Transactions);

        Assert.Equal("Test Source", savedTransaction.SourceSystem);
        Assert.Equal(100001, savedTransaction.TransactionId);
        Assert.Equal("Test Product", savedTransaction.ProductName);
        Assert.Equal(25.00m, savedTransaction.Amount);
        Assert.Equal(TransactionStatus.Active, savedTransaction.Status);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task ImportAsync_SameTransactionTwice_NoDuplicate()
    {
        // Arrange
        var transactionTime = TestNow.UtcDateTime;

        var sourceTransaction = new SourceTransaction
        {
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = transactionTime
        };

        var source = new FakeTransactionSource(
            "Test Source",
            new[] { sourceTransaction });

        var repository = new FakeTransactionRepository();

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            TimeProvider.System);

        // Act
        var firstResult = await service.ImportAsync();
        var secondResult = await service.ImportAsync();

        // Assert
        Assert.Equal(1, firstResult.NewTransactions);

        Assert.Equal(0, secondResult.NewTransactions);
        Assert.Equal(0, secondResult.UpdatedTransactions);
        Assert.Equal(1, secondResult.UnchangedTransactions);

        Assert.Single(repository.Transactions);
        Assert.Empty(repository.Transactions[0].AuditHistory);
    }

    [Fact]
    public async Task ImportAsync_ChangedAmount_UpdateTransactionAndCreateAudit()
    {
        // Arrange
        var transactionTime = TestNow.UtcDateTime;

        var existingTransaction = new TransactionRecord
        {
            SourceSystem = "Test Source",
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = transactionTime,
            Status = TransactionStatus.Active
        };

        var repository = new FakeTransactionRepository();
        repository.Transactions.Add(existingTransaction);

        var changedSourceTransaction = new SourceTransaction
        {
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 35.00m,
            TransactionTime = transactionTime
        };

        var source = new FakeTransactionSource(
            "Test Source",
            new[] { changedSourceTransaction });

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            new FixedTimeProvider(TestNow));

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(0, result.NewTransactions);
        Assert.Equal(1, result.UpdatedTransactions);
        Assert.Equal(0, result.UnchangedTransactions);

        Assert.Equal(35.00m, existingTransaction.Amount);

        var audit = Assert.Single(existingTransaction.AuditHistory);

        Assert.Equal(nameof(TransactionRecord.Amount), audit.FieldName);
        Assert.Equal(25.00m.ToString(), audit.OldValue);
        Assert.Equal(35.00m.ToString(), audit.NewValue);
    }

    [Fact]
    public async Task ImportAsync_RecentMissingTransaction_RevokeTransaction()
    {
        // Arrange
        var existingTransaction = new TransactionRecord
        {
            SourceSystem = "Test Source",
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,

            // Recent enough to be inside the 24-hour window
            TransactionTime = DateTime.UtcNow.AddHours(-4),

            Status = TransactionStatus.Active
        };

        var repository = new FakeTransactionRepository();
        repository.Transactions.Add(existingTransaction);

        // The source responded successfully but returned no transactions.
        var source = new FakeTransactionSource(
            "Test Source",
            Array.Empty<SourceTransaction>());

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            TimeProvider.System);

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(1, result.RevokedTransactions);
        Assert.Equal(TransactionStatus.Revoked, existingTransaction.Status);

        var audit = Assert.Single(existingTransaction.AuditHistory);

        Assert.Equal(nameof(TransactionRecord.Status), audit.FieldName);
        Assert.Equal(
            TransactionStatus.Active.ToString(),
            audit.OldValue);
        Assert.Equal(
            TransactionStatus.Revoked.ToString(),
            audit.NewValue);
    }

    [Fact]
    public async Task ImportAsync_OldMissingTransaction_FinalizedTransaction()
    {
        //var now = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);
        // Arrange
        var existingTransaction = new TransactionRecord
        {
            SourceSystem = "Test Source",
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = TestNow.UtcDateTime.AddHours(-25),
            Status = TransactionStatus.Active
        };

        var repository = new FakeTransactionRepository();
        repository.Transactions.Add(existingTransaction);

        var source = new FakeTransactionSource(
            "Test Source",
            Array.Empty<SourceTransaction>());

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
             new FixedTimeProvider(TestNow));

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(0, result.RevokedTransactions);
        Assert.Equal(1, result.FinalizedTransactions);
        Assert.Equal(TransactionStatus.Finalized, existingTransaction.Status);

        var audit = Assert.Single(existingTransaction.AuditHistory);
        Assert.Equal(nameof(TransactionRecord.Status), audit.FieldName);
        Assert.Equal(TransactionStatus.Active.ToString(), audit.OldValue);
        Assert.Equal(TransactionStatus.Finalized.ToString(), audit.NewValue);
        Assert.Equal(TestNow.UtcDateTime, audit.ChangedTimeUtc);
    }


    [Fact]
    public async Task ImportAsync_SourceFails_DoesNotRevokeTransaction()
    {
        // Arrange
        var existingTransaction = new TransactionRecord
        {
            SourceSystem = "Test Source",
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = TestNow.UtcDateTime.AddHours(-1),
            Status = TransactionStatus.Active
        };

        var repository = new FakeTransactionRepository();
        repository.Transactions.Add(existingTransaction);

        var source = new FailingTransactionSource("Test Source");

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            TimeProvider.System);

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(0, result.SuccessfulSources);
        Assert.Equal(1, result.FailedSources);
        Assert.Equal(0, result.RevokedTransactions);
        Assert.Single(result.Errors);

        Assert.Equal(TransactionStatus.Active, existingTransaction.Status);
        Assert.Empty(existingTransaction.AuditHistory);
    }

    [Fact]
    public async Task ImportAsync_OldActiveTransaction_FinalizesWithoutUpdating()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);
        var transactionTime = now.UtcDateTime.AddHours(-25);

        var existingTransaction = new TransactionRecord
        {
            SourceSystem = "Test Source",
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = transactionTime,
            Status = TransactionStatus.Active
        };

        var sourceTransaction = new SourceTransaction
        {
            TransactionId = existingTransaction.TransactionId,
            CardNumber = existingTransaction.CardNumber,
            LocationCode = existingTransaction.LocationCode,
            ProductName = existingTransaction.ProductName,
            Amount = 30.00m,
            TransactionTime = transactionTime
        };

        var repository = new FakeTransactionRepository();
        repository.Transactions.Add(existingTransaction);

        var source = new FakeTransactionSource(
            existingTransaction.SourceSystem,
            new[] { sourceTransaction });

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            new FixedTimeProvider(now));

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(25.00m, existingTransaction.Amount);
        Assert.Equal(TransactionStatus.Finalized, existingTransaction.Status);
        Assert.Equal(1, result.FinalizedTransactions);
        Assert.Equal(0, result.UpdatedTransactions);
        Assert.Equal(0, result.RevokedTransactions);

        var audit = Assert.Single(existingTransaction.AuditHistory);
        Assert.Equal(nameof(TransactionRecord.Status), audit.FieldName);
        Assert.Equal(TransactionStatus.Active.ToString(), audit.OldValue);
        Assert.Equal(TransactionStatus.Finalized.ToString(), audit.NewValue);
        Assert.Equal(now.UtcDateTime, audit.ChangedTimeUtc);
    }

    [Fact]
    public async Task ImportAsync_SameTransactionIdFromDifferentSources_CreatesTwoTransactions()
    {
        // Arrange
        var sourceTransactionA = new SourceTransaction
        {
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "LOCATION-A",
            ProductName = "Product A",
            Amount = 25.00m,
            TransactionTime = TestNow.UtcDateTime
        };

        var sourceTransactionB = new SourceTransaction
        {
            TransactionId = 100001,
            CardNumber = "2222",
            LocationCode = "LOCATION-B",
            ProductName = "Product B",
            Amount = 35.00m,
            TransactionTime = TestNow.UtcDateTime
        };

        var sourceA = new FakeTransactionSource(
            "Source A",
            new[] { sourceTransactionA });

        var sourceB = new FakeTransactionSource(
            "Source B",
            new[] { sourceTransactionB });

        var repository = new FakeTransactionRepository();

        var service = new ImportService(
            new ITransactionSource[] { sourceA, sourceB },
            repository,
            NullLogger<ImportService>.Instance,
            new FixedTimeProvider(TestNow));

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(2, result.NewTransactions);
        Assert.Equal(0, result.UpdatedTransactions);
        Assert.Equal(2, result.SuccessfulSources);

        Assert.Equal(2, repository.Transactions.Count);

        Assert.Contains(
            repository.Transactions,
            transaction =>
                transaction.SourceSystem == "Source A" &&
                transaction.TransactionId == 100001);

        Assert.Contains(
            repository.Transactions,
            transaction =>
                transaction.SourceSystem == "Source B" &&
                transaction.TransactionId == 100001);
    }

    [Theory]
    [InlineData(1439, TransactionStatus.Revoked, 1, 0)]
    [InlineData(1440, TransactionStatus.Revoked, 1, 0)]
    [InlineData(1441, TransactionStatus.Finalized, 0, 1)]
    public async Task ImportAsync_MissingTransaction_AppliesExactTwentyFourHourRule(
    int ageInMinutes,
    TransactionStatus expectedStatus,
    int expectedRevoked,
    int expectedFinalized)
    {
        // Arrange
        var existingTransaction = new TransactionRecord
        {
            SourceSystem = "Test Source",
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = TestNow
                .AddMinutes(-ageInMinutes)
                .UtcDateTime,
            Status = TransactionStatus.Active
        };

        var repository = new FakeTransactionRepository();
        repository.Transactions.Add(existingTransaction);

        var source = new FakeTransactionSource(
            "Test Source",
            Array.Empty<SourceTransaction>());

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            new FixedTimeProvider(TestNow));

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(expectedStatus, existingTransaction.Status);
        Assert.Equal(expectedRevoked, result.RevokedTransactions);
        Assert.Equal(expectedFinalized, result.FinalizedTransactions);

        var audit = Assert.Single(existingTransaction.AuditHistory);

        Assert.Equal(nameof(TransactionRecord.Status), audit.FieldName);
        Assert.Equal(TransactionStatus.Active.ToString(), audit.OldValue);
        Assert.Equal(expectedStatus.ToString(), audit.NewValue);
        Assert.Equal(TestNow.UtcDateTime, audit.ChangedTimeUtc);
    }


    [Fact]
    public async Task ImportAsync_RevokedTransactionReappearsWithinTwentyFourHours_ReactivatesAndCreatesAudit()
    {
        // Arrange
        var transactionTime = TestNow
            .AddHours(-4)
            .UtcDateTime;

        var existingTransaction = new TransactionRecord
        {
            SourceSystem = "Test Source",
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = transactionTime,
            Status = TransactionStatus.Revoked
        };

        var repository = new FakeTransactionRepository();
        repository.Transactions.Add(existingTransaction);

        // The same transaction has reappeared in the source.
        var sourceTransaction = new SourceTransaction
        {
            TransactionId = 100001,
            CardNumber = "1111",
            LocationCode = "TEST",
            ProductName = "Test Product",
            Amount = 25.00m,
            TransactionTime = transactionTime
        };

        var source = new FakeTransactionSource(
            "Test Source",
            new[] { sourceTransaction });

        var service = new ImportService(
            new[] { source },
            repository,
            NullLogger<ImportService>.Instance,
            new FixedTimeProvider(TestNow));

        // Act
        var result = await service.ImportAsync();

        // Assert
        Assert.Equal(TransactionStatus.Active, existingTransaction.Status);

        Assert.Equal(0, result.NewTransactions);
        Assert.Equal(1, result.UpdatedTransactions);
        Assert.Equal(0, result.UnchangedTransactions);
        Assert.Equal(0, result.RevokedTransactions);
        Assert.Equal(0, result.FinalizedTransactions);

        var audit = Assert.Single(existingTransaction.AuditHistory);

        Assert.Equal(nameof(TransactionRecord.Status), audit.FieldName);
        Assert.Equal(TransactionStatus.Revoked.ToString(), audit.OldValue);
        Assert.Equal(TransactionStatus.Active.ToString(), audit.NewValue);
        Assert.Equal(TestNow.UtcDateTime, audit.ChangedTimeUtc);
    }

    private sealed class FakeTransactionSource : ITransactionSource
    {
        private readonly IReadOnlyCollection<SourceTransaction> _transactions;

        public FakeTransactionSource(
            string sourceSystem,
            IReadOnlyCollection<SourceTransaction> Transactions)
        {
            SourceSystem = sourceSystem;
            _transactions = Transactions;
        }

        public string SourceSystem { get; }

        public Task<IReadOnlyCollection<SourceTransaction>> GetTransactionsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_transactions);
        }
    }

    private sealed class FakeTransactionRepository : ITransactionRepository
    {
        public List<TransactionRecord> Transactions { get; } = new();

        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyCollection<TransactionRecord>>
            GetBySourceSystemAsync(
                string sourceSystem,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<TransactionRecord> transactions = Transactions
                .Where(transactions =>
                    transactions.SourceSystem == sourceSystem)
                .ToList();

            return Task.FromResult(transactions);
        }

        public Task<IReadOnlyCollection<TransactionRecord>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<TransactionRecord> transactions =
                Transactions.ToList();

            return Task.FromResult(transactions);
        }

        public Task AddAsync(
            TransactionRecord transaction,
            CancellationToken cancellationToken = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }     
    }
    private sealed class FailingTransactionSource : ITransactionSource
    {
        public FailingTransactionSource(string sourceSystem)
        {
            SourceSystem = sourceSystem;
        }

        public string SourceSystem { get; }

        public Task<IReadOnlyCollection<SourceTransaction>> GetTransactionsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<IReadOnlyCollection<SourceTransaction>>(
                new Exception("Simulated source failure."));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
