using System;
using System.Collections.Generic;
using System.Text;
using TransactIngest.Application.Models;

namespace TransactIngest.Application.Interfaces
{
    public interface IImportService
    {
        Task<ImportResult> ImportAsync(
            CancellationToken cancellationToken = default);
    }
}
