using System.Net;

public class FailingDelegatingHandler : DelegatingHandler
{
    private static readonly double FaultResponseProbability = 0.8; // 80% chance of failed response

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var isFaultResponse = Random.Shared.NextDouble() <= FaultResponseProbability;
        if (isFaultResponse)
        {
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
