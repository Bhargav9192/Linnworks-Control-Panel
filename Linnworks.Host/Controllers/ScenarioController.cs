using Microsoft.AspNetCore.Mvc;
using LinnworksMacro.Orders;

[ApiController]
[Route("api/scenario")]
public class ScenarioController : ControllerBase
{
    private readonly Rishvi_CreateOrder_with_Scenarios _service;

    public ScenarioController(Rishvi_CreateOrder_with_Scenarios service)
    {
        _service = service;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] ScenarioRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Scenario))
            return BadRequest(new { success = false, message = "Scenario is required." });

        try
        {
            await _service.RunAsync(request.Scenario, request.Commit, request.UserAccount);

            return Ok(new
            {
                success = true,
                message = "Scenario executed successfully",
                scenarioName = request.Scenario,
                isCommitted = request.Commit
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
        }
    }
}

public class ScenarioRequest
{
    public string Scenario { get; set; }
    public bool Commit { get; set; }
    public string UserAccount { get; set; }
}