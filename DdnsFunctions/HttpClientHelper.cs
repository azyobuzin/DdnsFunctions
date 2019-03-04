using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace DdnsFunctions
{
    internal static class HttpClientHelper
    {
        public static HttpClient SharedHttpClient { get; } = new HttpClient();

        private static readonly Encoding s_utf8 = new UTF8Encoding(false, true);

        public static HttpContent CreateJsonContent(object value)
        {
            return new StringContent(
                JsonConvert.SerializeObject(value),
                s_utf8,
                "application/json"
            );
        }
    }
}
