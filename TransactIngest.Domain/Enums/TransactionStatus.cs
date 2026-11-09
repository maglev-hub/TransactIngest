using System;
using System.Collections.Generic;
using System.Text;

namespace TransactIngest.Domain.Enums
{
    public enum TransactionStatus
    {
        Active = 1,
        Revoked = 2,
        Finalized = 3
    }
}
