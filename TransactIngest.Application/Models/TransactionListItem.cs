using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Models;

namespace TransactIngest.Application.Models
{
    public class TransactionListItem
    {
        public string SourceSystem { get; set; } = string.Empty;

        public int TransactionId { get; set; }

        public string LocationCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime TransactionTime { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
