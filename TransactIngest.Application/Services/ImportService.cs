using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Interfaces;
using TransactIngest.Application.Models;
using TransactIngest.Domain.Entities;
using TransactIngest.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TransactIngest.Application.Services
{
    public class ImportService : IImportService
    {


        private readonly IReadOnlyCollection<ITransactionSource> _sources;
        private readonly ITransactionRepository _repository;
        private readonly ILogger<ImportService> _logger;

        private readonly TimeProvider _timeProvider;


        public ImportService(
            IEnumerable<ITransactionSource> sources,
            ITransactionRepository repository,
            ILogger<ImportService> logger,
            TimeProvider timeProvider)

        {
            _sources = sources.ToList();
            _repository = repository;
            _logger = logger;
            _timeProvider = timeProvider;
        }


        public async Task<ImportResult> ImportAsync(
            CancellationToken cancellationToken = default)
        {

            var stopwatch = Stopwatch.StartNew();

            var result = new ImportResult();
            var currentTimeUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var revocationCutoff = currentTimeUtc.AddHours(-24);

            _logger.LogInformation(
                "Transaction import started at {StartTimeUtc}.",
                currentTimeUtc);            

            foreach (var source in _sources)
            {
                try
                {
                    _logger.LogInformation(
                      "Starting transaction import for source {SourceSystem}.",
                      source.SourceSystem);

                    //Retrieve the latest source snapshot
                    var sourceTransactions =
                        await source.GetTransactionsAsync(cancellationToken);

                    _logger.LogInformation(
                        "Retrieved {TransactionCount} transactions from source {SourceSystem}.",
                        sourceTransactions.Count,
                        source.SourceSystem);

                    //Load existing transactions for only that source
                    var existingTransactions =
                        await _repository.GetBySourceSystemAsync(
                            source.SourceSystem,
                            cancellationToken);

                    // Build a transaction lookup dictionary
                    var existingByTransactionId =
                        existingTransactions.ToDictionary(
                            transaction => transaction.TransactionId);



                    //Compares each imported source transaction with stored transactions to determine if it is new, updated, or unchanged.
                    foreach (var sourceTransaction in sourceTransactions)
                    {
                        //If the transaction already exists, update it if any fields have changed.
                        if (existingByTransactionId.TryGetValue(
                          sourceTransaction.TransactionId,
                          out var existingTransaction))
                        {
                            var isPastUpdateWindow =  existingTransaction.TransactionTime < revocationCutoff;

                            // If the transaction is finalized or past the update window, it is considered unchanged.

                            if (existingTransaction.Status == TransactionStatus.Finalized ||
                                    isPastUpdateWindow)
                            {
                                result.UnchangedTransactions++;
                                continue;
                            }
                            var transactionChanged = UpdateExistingTransaction(
                                existingTransaction,
                                sourceTransaction,
                                currentTimeUtc);

                            if (transactionChanged)
                            {
                                result.UpdatedTransactions++;

                                _logger.LogDebug(
                                    "Updated transaction {TransactionId} from source {SourceSystem}.",
                                    existingTransaction.TransactionId,
                                    existingTransaction.SourceSystem);
                            }
                            else
                            {
                                result.UnchangedTransactions++;
                            }

                            continue;
                        }

                        //If the transaction does not exist, create a new transaction record.
                        var transaction = new TransactionRecord
                        {
                            SourceSystem = source.SourceSystem,
                            TransactionId = sourceTransaction.TransactionId,
                            CardNumber = sourceTransaction.CardNumber,
                            LocationCode = sourceTransaction.LocationCode,
                            ProductName = sourceTransaction.ProductName,
                            Amount = sourceTransaction.Amount,
                            TransactionTime = sourceTransaction.TransactionTime,
                            Status = sourceTransaction.TransactionTime < revocationCutoff
                                    ? TransactionStatus.Finalized
                                    : TransactionStatus.Active
                        };

                        //Add the new transaction to the repository and update the lookup dictionary.
                        await _repository.AddAsync(
                            transaction,
                            cancellationToken);

                        existingByTransactionId.Add(
                            transaction.TransactionId,
                            transaction);

                        result.NewTransactions++;

                        _logger.LogDebug(
                            "Added transaction {TransactionId} from source {SourceSystem}.",
                            transaction.TransactionId,
                            transaction.SourceSystem);
                    }

                    var sourceTransactionIds = sourceTransactions
                    .Select(transaction => transaction.TransactionId)
                    .ToHashSet();

                   

                    // Check existing transaction missing from this snapshot.
                    foreach (var transaction in existingByTransactionId.Values)
                    {
                        if (transaction.Status != TransactionStatus.Active)
                        {
                            continue;
                        }

                        TransactionStatus newStatus;
                        // If the transaction is older than 24 hours, finalize it. Otherwise, revoke it.

                        if (transaction.TransactionTime < revocationCutoff)
                        {
                            newStatus = TransactionStatus.Finalized;
                            result.FinalizedTransactions++;
                        }
                        // If the transaction is within the last 24 hours and not present in the source or missing in the snapshot, revoke it.
                        else if (transaction.TransactionTime <= currentTimeUtc &&
                                 !sourceTransactionIds.Contains(transaction.TransactionId))
                        {
                            newStatus = TransactionStatus.Revoked;
                            result.RevokedTransactions++;
                        }
                        else
                        {
                            continue;
                        }

                        RecordChange(
                            transaction,
                            nameof(TransactionRecord.Status),
                            transaction.Status.ToString(),
                            newStatus.ToString(),
                            currentTimeUtc);

                        transaction.Status = newStatus;

                        _logger.LogDebug(
                            "Changed transaction {TransactionId} from source {SourceSystem} to {Status}.",
                            transaction.TransactionId,
                            transaction.SourceSystem,
                            newStatus);
                    }

                    result.SuccessfulSources++;


                    _logger.LogInformation(
                        "Successfully processed source {SourceSystem}.",
                        source.SourceSystem);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning(
                        "Transaction import was cancelled while processing source {SourceSystem}.",
                        source.SourceSystem);
                    throw;
                }
                catch (Exception exception)
                {
                    result.FailedSources++;

                    result.Errors.Add(
                        $"{source.SourceSystem}: {exception.Message}");

                    _logger.LogError(
                           exception,
                           "Transaction import failed for source {SourceSystem}.",
                           source.SourceSystem);
                }
            }

            try
            {
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Transaction import was cancelled while saving database changes.");

                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to save transaction import changes.");

                throw;
            }

            _logger.LogInformation(
                  "Transaction import completed in {ElapsedMilliseconds} ms. " +
                  "Successful sources: {SuccessfulSources}; " +
                  "Failed sources: {FailedSources}; " +
                  "New transactions: {NewTransactions}; " +
                  "Updated transactions: {UpdatedTransactions}; " +
                  "Revoked transactions: {RevokedTransactions}; " +
                  "Unchanged transactions: {UnchangedTransactions}.",
                  stopwatch.ElapsedMilliseconds,
                  result.SuccessfulSources,
                  result.FailedSources,
                  result.NewTransactions,
                  result.UpdatedTransactions,
                  result.RevokedTransactions,
                  result.UnchangedTransactions);

            return result;
        }
        private static bool UpdateExistingTransaction(
        TransactionRecord transaction,
        SourceTransaction sourceTransactions,
        DateTime currentTimeUtc)
        {
            var changed = false;

            if (transaction.CardNumber != sourceTransactions.CardNumber)
            {
                RecordChange(
                    transaction,
                    nameof(TransactionRecord.CardNumber),
                    transaction.CardNumber,
                    sourceTransactions.CardNumber, 
                    currentTimeUtc);

                transaction.CardNumber = sourceTransactions.CardNumber;
                changed = true;
            }

            if (transaction.LocationCode != sourceTransactions.LocationCode)
            {
                RecordChange(
                    transaction,
                    nameof(TransactionRecord.LocationCode),
                    transaction.LocationCode,
                    sourceTransactions.LocationCode,
                    currentTimeUtc);

                transaction.LocationCode = sourceTransactions.LocationCode;
                changed = true;
            }

            if (transaction.ProductName != sourceTransactions.ProductName)
            {
                RecordChange(
                    transaction,
                    nameof(TransactionRecord.ProductName),
                    transaction.ProductName,
                    sourceTransactions.ProductName,
                    currentTimeUtc);

                transaction.ProductName = sourceTransactions.ProductName;
                changed = true;
            }

            if (transaction.Amount != sourceTransactions.Amount)
            {
                RecordChange(
                    transaction,
                    nameof(TransactionRecord.Amount),
                    transaction.Amount.ToString(),
                    sourceTransactions.Amount.ToString(),
                    currentTimeUtc);

                transaction.Amount = sourceTransactions.Amount;
                changed = true;
            }

            if (transaction.TransactionTime != sourceTransactions.TransactionTime)
            {
                RecordChange(
                    transaction,
                    nameof(TransactionRecord.TransactionTime),
                    transaction.TransactionTime.ToString(),
                    sourceTransactions.TransactionTime.ToString(),
                    currentTimeUtc);

                transaction.TransactionTime = sourceTransactions.TransactionTime;
                changed = true;
            }

            /*
            if (transaction.Status != TransactionStatus.Active)
            {
                RecordChange(
                    transaction,
                    nameof(TransactionRecord.Status),
                    transaction.Status.ToString(),
                    TransactionStatus.Active.ToString());

                transaction.Status = TransactionStatus.Active;
                changed = true;
            }
            */
            if (transaction.Status == TransactionStatus.Revoked)
            {
                RecordChange(
                    transaction,
                    nameof(TransactionRecord.Status),
                    TransactionStatus.Revoked.ToString(),
                    TransactionStatus.Active.ToString(),
                    currentTimeUtc);

                transaction.Status = TransactionStatus.Active;
                changed = true;
            }
            return changed;
        }

        private static void RecordChange(
            TransactionRecord transaction,
            string fieldName,
            string? oldValue,
            string? newValue,
            DateTime changedTimeUtc)
        {
            transaction.AuditHistory.Add(new TransactionAudit
            {
                TransactionRecordId = transaction.TransactionRecordId,
                TransactionRecord = transaction,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedTimeUtc = changedTimeUtc
            });
        }
    }


}
