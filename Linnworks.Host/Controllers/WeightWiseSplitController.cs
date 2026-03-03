using Microsoft.AspNetCore.Mvc;
using LinnworksMacro.Orders;

[ApiController]
[Route("api/weightsplit")]
public class WeightSplitController : ControllerBase
{
    private readonly Rishvi_WeighWise_Order_Split_Engine _service;

    public WeightSplitController(Rishvi_WeighWise_Order_Split_Engine service)
    {
        _service = service;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] WeightSplitRequest request)
    {
        try
        {
            // Service mathi actual count lavo
            int splitCount = await _service.RunAsync(request.numOrderIds, request.MaxAllowedKg);

            return Ok(new
            {
                success = true,
                message = splitCount > 0 ? "Weight-based split executed successfully." : "No orders exceeded the weight limit.",
                processedOrders = splitCount,
                thresholdUsed = request.MaxAllowedKg
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
        }
    }
}

public class WeightSplitRequest
{
    public int[] numOrderIds { get; set; }
    public double MaxAllowedKg { get; set; }
}