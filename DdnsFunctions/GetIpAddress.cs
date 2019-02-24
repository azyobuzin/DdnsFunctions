using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace DdnsFunctions
{
    public static class GetIpAddress
    {
        [FunctionName("GetIpAddress")]
        public static string Run([HttpTrigger(AuthorizationLevel.Function)] HttpRequest req)
        {
            return req.HttpContext.Connection.RemoteIpAddress.ToString();
        }
    }
}
