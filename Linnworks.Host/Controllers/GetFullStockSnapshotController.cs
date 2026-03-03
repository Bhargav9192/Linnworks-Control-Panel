using Microsoft.AspNetCore.Mvc;
using LinnworksMacro;

[ApiController]
[Route("api/stocksnapshot")]
public class StockSnapshotController : ControllerBase
{
    private readonly Rishvi_GetFullStockSnapshot _service;

    public StockSnapshotController(Rishvi_GetFullStockSnapshot service)
    {
        _service = service;
    }
    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] SnapshotRequest request)
    {
        string rootPath = Directory.GetCurrentDirectory();
        string snapshotPath = Path.Combine(rootPath, "SnapshotFile");

        await _service.RunAsync(request.UserAccount, snapshotPath);

        return Ok(new
        {
            success = true,
            message = $"Stock Snapshot for '{request.UserAccount}' Generated Successfully"
        });
    }
    public class SnapshotRequest
    {
        public string UserAccount { get; set; }
    }
}