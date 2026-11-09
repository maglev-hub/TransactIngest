using Microsoft.AspNetCore.Mvc;
using TransactIngest.Application.Models;
using TransactIngest.Application.Interfaces;

namespace TransactIngest.Controllers
{
    [ApiController]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private readonly IImportService _importService;

        public ImportController(IImportService importService)
        {
            _importService = importService;
        }

        [HttpPost]
        public async Task<ActionResult<ImportResult>> Import(
            CancellationToken cancellationToken)
        {
            var result = await _importService.ImportAsync(
                cancellationToken);

            return Ok(result);
        }
    }
}
