using System;
using System.Collections.Generic;
using System.Text;

namespace TransactIngest.Application.Models
{
    public class ImportResult
    {
        public int SuccessfulSources { get; set; }

        public int FailedSources { get; set; }

        public int NewTransactions { get; set; }

        public int UpdatedTransactions { get; set; }

        public int RevokedTransactions { get; set; }

        public int UnchangedTransactions { get; set; }

        public int FinalizedTransactions { get; set; }

        public List<string> Errors { get; set; } = new List<string>();
    }
}
