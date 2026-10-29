using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CrimeInsight.Api.Services;

public sealed record FetchedDataset(IReadOnlyList<JsonElement> Rows, int TotalCount);
public sealed class UpstreamDataException(string message) : Exception(message);

public sealed class OdcloudClient(HttpClient client, IConfiguration config)
{
    public async Task<FetchedDataset> FetchYearAsync(int year, CancellationToken ct)
    {
        if (!DatasetCatalog.Paths.TryGetValue(year, out var path))
            throw new ArgumentOutOfRangeException(nameof(year), "지원하지 않는 연도입니다.");

        var key = config["OpenData:ServiceKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new UpstreamDataException("OpenData__ServiceKey 환경변수가 없습니다.");
        // The portal may provide a percent-encoded key. Decode once for the Infuser header.
        var decodedKey = Uri.UnescapeDataString(key.Trim());
        var pageSize = Math.Clamp(config.GetValue("OpenData:PageSize", 100), 1, 1000);
        var output = new List<JsonElement>();
        var total = -1;
        for (var page = 1; page <= 100; page++)
        {

            var url = $"{path}?page={page}&perPage={pageSize}&returnType=JSON";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Authorization = new AuthenticationHeaderValue("Infuser", decodedKey);

            using var response = await SendWithRetryAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new UpstreamDataException("공공데이터 API 인증이 거부되었습니다. 인증키와 활용승인을 확인하세요.");

            if (!response.IsSuccessStatusCode)
                throw new UpstreamDataException($"공공데이터 API HTTP {(int)response.StatusCode}");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                throw new UpstreamDataException("공공데이터 응답에서 data 배열을 찾을 수 없습니다.");

            if (!root.TryGetProperty("totalCount", out var totalElement) ||
                !totalElement.TryGetInt32(out var receivedTotal) || receivedTotal < 0)

                throw new UpstreamDataException("공공데이터 응답에서 totalCount를 확인할 수 없습니다.");


            if (total == -1) total = receivedTotal;

            if (total != receivedTotal) throw new UpstreamDataException("페이지 간 totalCount가 달라졌습니다. 재시도하세요.");
           
            var got = 0;

            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new UpstreamDataException("응답 행의 JSON 형식이 올바르지 않습니다.");
                output.Add(item.Clone());
                got++;
            }

            if (output.Count >= total) 
                break;

            if (got == 0) 
                throw new UpstreamDataException("전체 결과를 읽기 전에 빈 페이지가 반환되었습니다.");
        }
        if (total <= 0 || output.Count != total)
            throw new UpstreamDataException($"조회 건수 불일치: 기대 {total}, 수신 {output.Count}");
        return new FetchedDataset(output, total);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage template, CancellationToken ct)
    {
        // GET-only: new request per attempt because HttpRequestMessage cannot be reused.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, template.RequestUri);
            req.Headers.Authorization = template.Headers.Authorization;
            try
            {
                var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((((int)response.StatusCode == 429) || ((int)response.StatusCode >= 500)) && attempt < 2)
                {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromMilliseconds(450 * (attempt + 1)), ct);
                    continue;
                }
                return response;
            }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(450 * (attempt + 1)), ct);
            }
        }
        throw new UpstreamDataException("외부 API 요청을 여러 번 시도했지만 실패했습니다.");
    }
}
