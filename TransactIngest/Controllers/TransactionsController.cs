using Microsoft.AspNetCore.Mvc;
using TransactIngest.Application.Models;
using TransactIngest.Application.Interfaces;

namespace TransactIngest.Controllers
{
    [ApiController]
    [Route("api/transactions")]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionRepository _repository;

        public TransactionsController(
            ITransactionRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<
            IReadOnlyCollection<TransactionListItem>>> GetAll(
            CancellationToken cancellationToken)
        {
            var transactions = await _repository.GetAllAsync(
                cancellationToken);

            var result = transactions
                .Select(transaction => new TransactionListItem
                {
                    SourceSystem = transaction.SourceSystem,
                    TransactionId = transaction.TransactionId,
                    LocationCode = transaction.LocationCode,
                    ProductName = transaction.ProductName,
                    Amount = transaction.Amount,
                    TransactionTime = transaction.TransactionTime,
                    Status = transaction.Status.ToString()
                })
                .ToList();

            return Ok(result);
        }
    }
}