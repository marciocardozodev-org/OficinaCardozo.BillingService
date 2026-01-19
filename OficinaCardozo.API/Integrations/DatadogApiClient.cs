using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OficinaCardozo.API.Integrations
{
    public class DatadogApiClient
    {

        private readonly string _apiKey;
        private readonly string _metricsUrl;
        private readonly string _host;
        private readonly string _podName;

        public DatadogApiClient()
        {
            _apiKey = Environment.GetEnvironmentVariable("DATADOG_API_KEY") ?? throw new InvalidOperationException("DATADOG_API_KEY não definida");
            _metricsUrl = (Environment.GetEnvironmentVariable("DATADOG_METRICS_URL") ?? "https://api.datadoghq.com/api/v1/series") + "?api_key=" + _apiKey;
            _host = Environment.GetEnvironmentVariable("DATADOG_HOST") ?? "oficina-cardozo-api";
            _podName = Environment.GetEnvironmentVariable("POD_NAME") ?? string.Empty;
        }

        public async Task SendMetricAsync(string metricName, double value, string host, string[] tags)
        {
            using var client = new HttpClient();

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tagsList = new List<string>(tags ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(_podName))
            {
                tagsList.Add($"pod_name:{_podName}");
            }

            var payload = new
            {
                series = new[]
                {
                    new {
                        metric = metricName,
                        points = new[] { new object[] { timestamp, value } },
                        type = "gauge",
                        host = host ?? _host,
                        tags = tagsList.ToArray()
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_metricsUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            Serilog.Log.Information("Datadog response: StatusCode={StatusCode}, Body={Body}", response.StatusCode, responseBody);
            response.EnsureSuccessStatusCode();
        }
    }
}
