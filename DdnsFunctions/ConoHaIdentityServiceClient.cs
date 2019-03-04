using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static DdnsFunctions.HttpClientHelper;

namespace DdnsFunctions
{
    public class ConoHaIdentityServiceClient
    {
        private readonly string _endpoint;

        public ConoHaIdentityServiceClient(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentException("endpoint is empty.");

            this._endpoint = endpoint;
        }

        public async Task<JObject> GetTokensAsync(string username, string password, string tenantId)
        {
            var res = await SharedHttpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, this._endpoint + "/tokens")
                {
                    Headers =
                    {
                        Accept = { new MediaTypeWithQualityHeaderValue("application/json") },
                    },
                    Content = CreateJsonContent(new
                    {
                        auth = new
                        {
                            passwordCredentials = new { username, password },
                            tenantId,
                        }
                    }),
                }
            ).ConfigureAwait(false);

            using (res)
            {
                if (!res.IsSuccessStatusCode && string.Equals(res.Content?.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    // エラーレスポンス
                    throw new ConoHaApiException(await res.Content.ReadAsStringAsync().ConfigureAwait(false));
                }

                var s = await res.EnsureSuccessStatusCode().Content
                    .ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(s);
            }
        }
    }
}
