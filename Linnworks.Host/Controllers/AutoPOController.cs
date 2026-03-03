using Microsoft.AspNetCore.Mvc;
using LinnworksMacro.LinnworksTest;

[ApiController]
[Route("api/autopo")]
public class AutoPOController : ControllerBase
{
    private readonly Rishvi_AutoPO_OnOrderProcesing _service;

    public AutoPOController(Rishvi_AutoPO_OnOrderProcesing service)
    {
        _service = service;
    }
    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        try
        {
            // Have RunAsync counts return karshe
            var (orderCount, poCount) = await _service.RunAsync();

            return Ok(new
            {
                success = true,
                message = "Auto PO Macro Executed Successfully",
                poCount = poCount,
                orderCount = orderCount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
        }
    }
}