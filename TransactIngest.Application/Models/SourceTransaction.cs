using System;
using System.Collections.Generic;
using System.Text;

namespace TransactIngest.Application.Models
{
    public class SourceTransaction
    {
        public int TransactionId { get; set; }

        public string CardNumber { get; set; } = string.Empty;

        public string LocationCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime TransactionTime { get; set; }
    }
}
