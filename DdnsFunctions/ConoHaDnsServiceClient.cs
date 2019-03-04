using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static DdnsFunctions.HttpClientHelper;

namespace DdnsFunctions
{
    public class ConoHaDnsServiceClient
    {
        private readonly string _endpoint;
        private readonly string _token;

        public ConoHaDnsServiceClient(string endpoint, string token)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentException("endpoint is empty.");
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException("token is empty.");

            this._endpoint = endpoint;
            this._token = token;
        }

        public Task<JObject> GetDomainsAsync(string name = null)
        {
            var requestUri = this._endpoint + "/v1/domains";
            if (name != null) requestUri += "?name=" + Uri.EscapeDataString(name);

            return this.GetJsonResponseAsync(HttpMethod.Get, requestUri, null);
        }

        public Task<JObject> GetRecordsAsync(string domainId)
        {
            if (string.IsNullOrEmpty(domainId))
                throw new ArgumentException("domainId is empty.");

            var requestUri = this._endpoint + "/v1/domains/" + Uri.EscapeDataString(domainId) + "/records";

            return this.GetJsonResponseAsync(HttpMethod.Get, requestUri, null);
        }

        public Task<JObject> CreateRecordAsync(string domainId, object record)
        {
            if (string.IsNullOrEmpty(domainId))
                throw new ArgumentException("domainId is empty.");
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var requestUri = this._endpoint + "/v1/domains/" + Uri.EscapeDataString(domainId) + "/records";

            return this.GetJsonResponseAsync(HttpMethod.Post, requestUri, CreateJsonContent(record));
        }

        public Task<JObject> UpdateRecordAsync(string domainId, string recordId, object record)
        {
            if (string.IsNullOrEmpty(domainId))
                throw new ArgumentException("domainId is empty.");
            if (string.IsNullOrEmpty(recordId))
                throw new ArgumentException("recordId is empty.");
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var requestUri = this._endpoint + "/v1/domains/" + Uri.EscapeDataString(domainId)
                + "/records/" + Uri.EscapeDataString(recordId);

            return this.GetJsonResponseAsync(HttpMethod.Put, requestUri, CreateJsonContent(record));
        }

        private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string requestUri, HttpContent content)
        {
            var req = new HttpRequestMessage(method, requestUri)
            {
                Headers =
                {
                    Accept = { new MediaTypeWithQualityHeaderValue("application/json") },
                },
                Content = content,
            };

            req.Headers.TryAddWithoutValidation("X-Auth-Token", this._token);

            var res = await SharedHttpClient.SendAsync(req).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode && string.Equals(res.Content?.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                // エラーレスポンス
                using (res) throw new ConoHaApiException(await res.Content.ReadAsStringAsync().ConfigureAwait(false));
            }

            return res.EnsureSuccessStatusCode();
        }

        private async Task<JObject> GetJsonResponseAsync(HttpMethod method, string requestUri, HttpContent content)
        {
            using (var res = await this.SendRequestAsync(method, requestUri, content))
            {
                var s = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(s);
            }
        }
    }
}
