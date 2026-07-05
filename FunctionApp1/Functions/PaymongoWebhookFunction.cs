using FunctionApp1.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace FunctionApp1.Functions;

public class PaymongoWebhookFunction(PaymongoWebhookProcessor _processor)
{
    const string SignatureHeader = "Paymongo-Signature";

    [Function("PaymongoWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/paymongo")] HttpRequest req,
        CancellationToken ct)
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync(ct);

        var outcome = await _processor.ProcessAsync(body, req.Headers[SignatureHeader], ct);

        if (!outcome.Accepted)
            return new UnauthorizedResult();

        return new OkObjectResult(new { message = "SUCCESS", result = outcome.Result });
    }
}
