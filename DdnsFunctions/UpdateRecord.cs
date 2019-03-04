using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DdnsFunctions
{
    public static class UpdateRecord
    {
        private static readonly HttpClient s_httpClient = new HttpClient();

        [FunctionName("UpdateRecord_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [OrchestrationClient] DurableOrchestrationClient starter,
            ILogger log)
        {
            var httpContext = (HttpContext)req.Properties["HttpContext"];

            string GetParameter(string name)
            {
                var httpRequest = httpContext.Request;
                string parameterValue = null;
                if (httpRequest.HasFormContentType)
                    parameterValue = httpRequest.Form[name].LastOrDefault(x => !string.IsNullOrEmpty(x));
                return parameterValue ?? httpRequest.Query[name].LastOrDefault(x => !string.IsNullOrEmpty(x));
            }

            var domain = GetParameter("domain");
            var record = GetParameter("record");
            var value = GetParameter("value");

            if (string.IsNullOrEmpty(domain))
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "domain is not specified");
            if (string.IsNullOrEmpty(record))
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "record is not specified");

            IPAddress ipAddress;
            if (string.IsNullOrEmpty(value))
            {
                ipAddress = httpContext.Connection.RemoteIpAddress;
                value = ipAddress.ToString();
            }
            else
            {
                ipAddress = IPAddress.Parse(value);
            }

            bool isIpv6;
            switch (ipAddress.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    isIpv6 = false;
                    break;
                case AddressFamily.InterNetworkV6:
                    isIpv6 = true;
                    break;
                default:
                    throw new Exception($"{ipAddress} ({ipAddress.AddressFamily})");
            }

            const string identityServiceEndpointKey = "DDNSFUNCTIONS_CONOHA_IDENTITY_SERVICE_ENDPOINT";
            const string userNameKey = "DDNSFUNCTIONS_CONOHA_USERNAME";
            const string passwordKey = "DDNSFUNCTIONS_CONOHA_PASSWORD";
            const string tenantIdKey = "DDNSFUNCTIONS_CONOHA_TENANTID";

            var identityServiceEndpoint = Environment.GetEnvironmentVariable(identityServiceEndpointKey);
            var userName = Environment.GetEnvironmentVariable(userNameKey);
            var password = Environment.GetEnvironmentVariable(passwordKey);
            var tenantId = Environment.GetEnvironmentVariable(tenantIdKey);

            if (string.IsNullOrEmpty(identityServiceEndpoint))
                throw new Exception(identityServiceEndpointKey + " is not configured.");
            if (string.IsNullOrEmpty(userName))
                throw new Exception(userNameKey + " is not configured.");
            if (string.IsNullOrEmpty(password))
                throw new Exception(passwordKey + " is not configured.");

            var input = new UpdateRecordInput()
            {
                Domain = domain,
                Record = record,
                Type = isIpv6 ? "AAAA" : "A",
                Value = value,
                IdentityServiceEndpoint = identityServiceEndpoint,
                UserName = userName,
                Password = password,
                TenantId = tenantId,
            };

            var instanceId = await starter.StartNewAsync("UpdateRecord", input);

            log.LogInformation(
                "Start UpdateRecord (Record = {RecordName}, Type = {RecordType}, Value = {Value}, InstanceID = {InstanceID})",
                ToFullName(domain, record), input.Type, value, instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("UpdateRecord")]
        public static async Task RunOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var input = context.GetInput<UpdateRecordInput>();
            await context.CallActivityAsync("UpdateRecord_Core", input);
        }

        [FunctionName("UpdateRecord_Core")]
        public static async Task RunCore([ActivityTrigger] DurableActivityContext context, ILogger log)
        {
            var input = context.GetInput<UpdateRecordInput>();
            var fullName = ToFullName(input.Domain, input.Record);

            var ttlConfig = Environment.GetEnvironmentVariable("DDNSFUNCTIONS_TTL");
            if (string.IsNullOrEmpty(ttlConfig) || !int.TryParse(ttlConfig, out var ttl))
                ttl = 300;

            // アクセストークンを取得
            JObject tokenResponse;
            try
            {
                tokenResponse = await new ConoHaIdentityServiceClient(input.IdentityServiceEndpoint)
                    .GetTokensAsync(input.UserName, input.Password, input.TenantId);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "アクセストークンを取得できませんでした。");
                return;
            }

            var token = (string)tokenResponse["access"]["token"]["id"];

            var dnsServiceEndpoint = (string)tokenResponse["access"]["serviceCatalog"]
                .First(x => (string)x["type"] == "dns")["endpoints"]
                .First()["publicURL"];

            var dnsServiceClient = new ConoHaDnsServiceClient(dnsServiceEndpoint, token);

            // ドメインを探す
            var domains = (await dnsServiceClient.GetDomainsAsync(input.Domain))["domains"] as JArray;

            if (domains == null || domains.Count == 0)
            {
                log.LogError("ドメイン {Domain} が見つかりませんでした。", input.Domain);
                return;
            }
            if (domains.Count > 1)
            {
                log.LogError("ドメインが複数見つかりました: {Json}", domains.ToString(Formatting.Indented));
                return;
            }

            var domainId = (string)domains[0]["id"];

            // レコードを探す
            var existingRecord = (await dnsServiceClient.GetRecordsAsync(domainId))["records"]
                .Where(x => (string)x["name"] == fullName && (string)x["type"] == input.Type)
                .FirstOrDefault();

            const string description = "Configured by DdnsFunctions";

            if (existingRecord != null)
            {
                var recordId = (string)existingRecord["id"];

                if ((string)existingRecord["data"] == input.Value)
                {
                    // 更新不要
                    log.LogInformation("レコード {RecordId} はすでに {Value} が設定されています。", recordId, input.Value);
                }
                else
                {
                    // 更新
                    log.LogInformation("レコード {RecordId} に {Value} を設定します。", recordId, input.Value);

                    await dnsServiceClient.UpdateRecordAsync(
                        domainId, recordId,
                        new
                        {
                            data = input.Value,
                            ttl,
                            description,
                        }
                    );
                }
            }
            else
            {
                // 新規作成
                log.LogInformation(
                    "レコード {RecordName} {TTL} IN {RecordType} {Value} を作成します。",
                    fullName, ttl, input.Type, input.Value);

                await dnsServiceClient.CreateRecordAsync(
                    domainId,
                    new
                    {
                        name = fullName,
                        type = input.Type,
                        data = input.Value,
                        ttl,
                        description,
                    }
                );
            }
        }

        private static string ToFullName(string domain, string record)
        {
            return record == "@" ? domain : record + "." + domain;
        }

        public class UpdateRecordInput
        {
            public string Domain { get; set; }
            public string Record { get; set; }
            public string Type { get; set; }
            public string Value { get; set; }
            public string IdentityServiceEndpoint { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public string TenantId { get; set; }
        }
    }
}
