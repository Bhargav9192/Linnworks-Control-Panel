using Microsoft.AspNetCore.Mvc;
using LinnworksMacro.Orders;

namespace LinnworksMacro.Controllers
{
    [ApiController]
    [Route("api/quantity-split")]
    public class QuantitySplitController : ControllerBase
    {
        private readonly Rishvi_Quantity_Based_Splitting _service;

        public QuantitySplitController(Rishvi_Quantity_Based_Splitting service)
        {
            _service = service;
        }

        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] QuantitySplitRequest request)
        {
            if (request == null || request.NumOrderIds == null || request.NumOrderIds.Length == 0)
                return BadRequest("NumOrderIds are required.");

            if (request.QuantityThreshold <= 0)
                return BadRequest("QuantityThreshold must be greater than 0.");

            try
            {
                int splitCount = await _service.RunAsync(request.NumOrderIds, request.QuantityThreshold);

                // CASE 1: Jo split na thayu hoy (Count 0 hoy)
                if (splitCount == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No splitting required. All orders are already within the limit.",
                        processedOrders = 0,
                        thresholdUsed = request.QuantityThreshold
                    });
                }

                // CASE 2: Jo split thayu hoy (Count > 0 hoy)
                // 🔥 Ahiya badlav karvo padshe:
                return Ok(new
                {
                    success = true,
                    message = "Quantity-based splitting executed successfully.", // Message badlo
                    processedOrders = splitCount, // 👈 Ahiya 'splitCount' variable vapro, '0' nahi
                    thresholdUsed = request.QuantityThreshold
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
    public class QuantitySplitRequest
    {
        public int[] NumOrderIds { get; set; }
        public int QuantityThreshold { get; set; }
    }
}