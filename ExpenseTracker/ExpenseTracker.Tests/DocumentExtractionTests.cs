using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ExpenseTracker.Options;
using ExpenseTracker.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Tests;

public sealed class DocumentExtractionTests
{
    [Fact]
    public void OptionsRejectMissingTaskIdTemplates()
    {
        var options = new DocumentExtractionOptions
        {
            BaseUrl = "http://127.0.0.1:5001",
            SubmitPath = "/submit",
            StatusPathTemplate = "/status",
            ResultPathTemplate = "/result"
        };

        Assert.Contains(options.Validate(new ValidationContext(options)), result => result.ErrorMessage!.Contains("{task_id}"));
    }

    [Fact]
    public async Task ProviderReturnsRawResultAfterAsyncPolling()
    {
        var calls = new List<string>();
        var rawJson = "{\"provider_field\": [1,2,3]}";
        var handler = new StubHandler(request =>
        {
            calls.Add(request.Method + " " + request.RequestUri!.PathAndQuery);
            return calls.Count switch
            {
                1 => JsonResponse("{\"task_id\":\"abc\"}"),
                2 => JsonResponse("{\"task_status\":\"success\"}"),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json")
                }
            };
        });
        var options = Microsoft.Extensions.Options.Options.Create(new DocumentExtractionOptions
        {
            BaseUrl = "http://provider",
            SubmitPath = "/submit",
            StatusPathTemplate = "/status/{task_id}",
            ResultPathTemplate = "/result/{task_id}",
            PollIntervalSeconds = 1,
            ExtractionDeadlineSeconds = 10
        });
        var provider = new DoclingDocumentExtractionProvider(new StubFactory(handler), options, NullLogger<DoclingDocumentExtractionProvider>.Instance);
        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, "%PDF-test");
            var result = await provider.ExtractAsync(path, CancellationToken.None);

            Assert.Equal(rawJson, System.Text.Encoding.UTF8.GetString(result.Body));
            Assert.Equal(["POST /submit", "GET /status/abc", "GET /result/abc"], calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("http://provider") };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }
}