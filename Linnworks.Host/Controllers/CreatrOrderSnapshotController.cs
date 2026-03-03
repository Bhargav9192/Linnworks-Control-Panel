using Microsoft.AspNetCore.Mvc;
using LinnworksMacro.Orders;

[ApiController]
[Route("api/ordersnapshot")]
public class OrderSnapshotController : ControllerBase
{
    private readonly CreateOrdersFromSnapshotService _service;

    public OrderSnapshotController(CreateOrdersFromSnapshotService service)
    {
        _service = service;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] SnapshotRequest request)
    {
        try
        {
            await _service.RunAsync(request.UserAccount, request.ValidOrders, request.InvalidOrders);

            return Ok(new
            {
                success = true,
                message = $"Orders for '{request.UserAccount}' Processed Successfully",
                validCount = request.ValidOrders,
                invalidCount = request.InvalidOrders
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
        }
    }
}

public class SnapshotRequest
{
    public string UserAccount { get; set; }
    public int ValidOrders { get; set; }
    public int InvalidOrders { get; set; }
}