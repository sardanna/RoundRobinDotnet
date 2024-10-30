using Microsoft.AspNetCore.Mvc;

namespace RoundRobinApplicationApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessController : ControllerBase
    {
        /// <summary>
        /// This is a dummy method which returns the json body it is recieving.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CheckRoundRobin()
        {
                using var reader = new StreamReader(Request.Body);
            var jsonBody = await reader.ReadToEndAsync();

            // Return the raw JSON as a content result with the correct content type
            return Content(jsonBody, "application/json");
        }
    }
}
