using System;
using System.Collections.Generic;
using System.Text;

namespace TransactIngest.Domain.Entities
{
    public class TransactionAudit
    {
        public long TransactionAuditId { get; set; }

        public long TransactionRecordId { get; set; }

        public string FieldName { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public DateTime ChangedTimeUtc { get; set; }

        public TransactionRecord TransactionRecord { get; set; } = null!;
    }
}
