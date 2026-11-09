using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Domain.Enums;

namespace TransactIngest.Domain.Entities
{
    public class TransactionRecord
    {
        public long TransactionRecordId { get; set; }

        public string SourceSystem { get; set; } = string.Empty;

        public int TransactionId { get; set; }

        public string CardNumber { get; set; } = string.Empty;

        public string LocationCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime TransactionTime { get; set; }

        public TransactionStatus Status { get; set; } = TransactionStatus.Active;
        public ICollection<TransactionAudit> AuditHistory { get; set; }
        = new List<TransactionAudit>();

    }

}
