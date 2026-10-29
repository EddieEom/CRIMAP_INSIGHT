using System.Net;
using System.Text;
using CrimeInsight.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CrimeInsight.Tests;

public class OdcloudClientTests
{
    [Fact]
    public async Task FetchYear_Paginates_Using_Infuser_Header_Without_CSV()
    {
        var calls = 0;
        var handler = new FakeHandler(request =>
        {
            Assert.Equal("Infuser", request.Headers.Authorization?.Scheme);
            Assert.Equal("DUMMY_API_KEY==", request.Headers.Authorization?.Parameter);
            var page = request.RequestUri?.Query.Contains("page=2") == true ? 2 : 1;
            calls++;
            var body = page == 1
                ? """{"totalCount":3,"currentCount":2,"data":[{"a":1},{"a":2}]}"""
                : """{"totalCount":3,"currentCount":1,"data":[{"a":3}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.odcloud.kr/api/") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenData:ServiceKey"] = "DUMMY_API_KEY%3D%3D",
            ["OpenData:PageSize"] = "2"
        }).Build();
        var dataset = await new OdcloudClient(client, config).FetchYearAsync(2025, CancellationToken.None);
        Assert.Equal(3, dataset.TotalCount);
        Assert.Equal(3, dataset.Rows.Count);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Rejects_Page_With_Inconsistent_Total_Count()
    {
        var handler = new FakeHandler(req =>
        {
            var page2 = req.RequestUri?.Query.Contains("page=2") == true;
            var body = page2
                ? """{"totalCount":99,"data":[{"a":3}]}"""
                : """{"totalCount":3,"data":[{"a":1},{"a":2}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.odcloud.kr/api/") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenData:ServiceKey"] = "DUMMY", ["OpenData:PageSize"] = "2"
        }).Build();
        await Assert.ThrowsAsync<UpstreamDataException>(() =>
            new OdcloudClient(client, config).FetchYearAsync(2025, CancellationToken.None));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
