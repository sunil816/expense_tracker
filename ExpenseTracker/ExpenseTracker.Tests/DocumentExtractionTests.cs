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
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Options;
using ExpenseTracker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharpCore.Pdf;

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
    public void ConversionOptionsAreReadFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentExtraction:ConversionOptions:from_formats:0"] = "pdf",
                ["DocumentExtraction:ConversionOptions:to_formats:0"] = "json",
                ["DocumentExtraction:ConversionOptions:do_ocr"] = "false",
                ["DocumentExtraction:ConversionOptions:do_table_structure"] = "true",
                ["DocumentExtraction:ConversionOptions:table_mode"] = "accurate"
            })
            .Build();

        var result = DocumentExtractionOptions.ReadConversionOptions(
            configuration.GetSection("DocumentExtraction:ConversionOptions"));

        Assert.Equal("pdf", result.GetProperty("from_formats")[0].GetString());
        Assert.Equal("json", result.GetProperty("to_formats")[0].GetString());
        Assert.False(result.GetProperty("do_ocr").GetBoolean());
        Assert.True(result.GetProperty("do_table_structure").GetBoolean());
        Assert.Equal("accurate", result.GetProperty("table_mode").GetString());
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

        [Fact]
        public void ParserMapsStructuredTransactionsAcrossContinuationTables()
        {
                var body = System.Text.Encoding.UTF8.GetBytes("""
                        {
                            "document": {
                                "json_content": {
                                    "tables": [
                                        {
                                            "data": {
                                                "grid": [
                                                    [{"text":"Name"},{"text":"Bank"},{"text":"Amount"},{"text":"Date"},{"text":"Status"}],
                                                    [{"text":"SHOP"},{"text":"Kotak 4603"},{"text":"-160.00"},{"text":"12 July 2026"},{"text":"SUCCESS"}]
                                                ]
                                            }
                                        },
                                        {
                                            "data": {
                                                "grid": [
                                                    [{"text":"CAFE"},{"text":"Kotak 4603"},{"text":"-20.00"},{"text":"11 July 2026"},{"text":"SUCCESS"}]
                                                ]
                                            }
                                        }
                                    ]
                                },
                                "md_content": null
                            }
                        }
                        """);

                var transactions = new TransactionResultParser().Parse(body);

                Assert.Equal(2, transactions.Count);
                Assert.Equal("SHOP", transactions[0].Description);
                Assert.Equal(160m, transactions[0].Amount);
                Assert.Equal(TransactionDirection.Debit, transactions[0].Direction);
                Assert.Equal("CAFE", transactions[1].Description);
        }

        [Fact]
        public void ParserRejectsFailedPaymentRows()
        {
                var body = System.Text.Encoding.UTF8.GetBytes("""
                        {
                            "document": {
                                "json_content": {
                                    "tables": [{
                                        "data": {
                                            "grid": [
                                                [{"text":"Name"},{"text":"Bank"},{"text":"Amount"},{"text":"Date"},{"text":"Status"}],
                                                [{"text":"SHOP"},{"text":"Kotak 4603"},{"text":"-160.00"},{"text":"12 July 2026"},{"text":"SUCCESS"}],
                                                [{"text":"CAFE"},{"text":"Kotak 4603"},{"text":"-20.00"},{"text":"11 July 2026"},{"text":"FAILED"}],
                                                [{"text":"MARKET"},{"text":"Kotak 4603"},{"text":"-40.00"},{"text":"10 July 2026"},{"text":"SUCCESS"}]
                                            ]
                                        }
                                    }]
                                },
                                "md_content": null
                            }
                        }
                        """);

                var transactions = new TransactionResultParser().Parse(body);

                Assert.Collection(
                    transactions,
                    transaction => Assert.Equal("SHOP", transaction.Description),
                    transaction => Assert.Equal("MARKET", transaction.Description));
        }

        [Fact]
        public void ParserMapsInstamartOrdersAndIgnoresCustomerSummary()
        {
            const string firstOrderLink = "https://example.test/orders/236616081015876.pdf";
            const string secondOrderLink = "https://example.test/orders/242359177004539.pdf";
                var body = System.Text.Encoding.UTF8.GetBytes("""
                        {
                            "document": {
                                "json_content": {
                                    "tables": [
                                        {
                                            "data": {
                                                "grid": [
                                                    [{"text":"Customer Name"},{"text":"Number of Orders"},{"text":"Total Amount"}],
                                                    [{"text":"sunil"},{"text":"13"},{"text":"₹4493.41"}]
                                                ]
                                            }
                                        },
                                        {
                                            "prov": [{"page_no":1}],
                                            "data": {
                                                "grid": [
                                                    [{"text":"Date / Time"},{"text":"Order ID"},{"text":"Pod Name"},{"text":"Amount"},{"text":"View"}],
                                                    [{"text":"01-05-2026"},{"text":"236616081015876"},{"text":"Spwave Pvt Ltd - Vishweshwara Nagara"},{"text":"₹758.62"},{"text":"View"}]
                                                ]
                                            }
                                        },
                                        {
                                            "prov": [{"page_no":2}],
                                            "data": {
                                                "grid": [
                                                    [{"text":"Date / Time"},{"text":"Order ID"},{"text":"Pod Name"},{"text":"Amount"},{"text":"View"}],
                                                    [{"text":"07-07-2026"},{"text":"242359177004539"},{"text":"Spwave Pvt Ltd - Kalamandir"},{"text":"₹522.00"},{"text":"View"}]
                                                ]
                                            }
                                        }
                                    ]
                                },
                                "md_content": null
                            }
                        }
                        """);

                var transactions = new TransactionResultParser().Parse(body, new Dictionary<int, IReadOnlyList<string>>
                {
                    [1] = [firstOrderLink],
                    [2] = [secondOrderLink]
                });

                Assert.Equal(2, transactions.Count);
                Assert.Equal("236616081015876", transactions[0].ExternalReference);
                Assert.Equal("Spwave Pvt Ltd - Vishweshwara Nagara", transactions[0].Description);
                Assert.Equal(758.62m, transactions[0].Amount);
                Assert.Equal(firstOrderLink, transactions[0].ReceiptUrl);
                Assert.Equal("242359177004539", transactions[1].ExternalReference);
                Assert.Equal(secondOrderLink, transactions[1].ReceiptUrl);
        }

        [Fact]
        public void ParserMapsOnlyTransactionRowsFromMarkdownTables()
        {
                const string markdown = """
                        | Savings Account Transactions | Savings Account Transactions | Savings Account Transactions | Savings Account Transactions | Savings Account Transactions | Savings Account Transactions | Savings Account Transactions |
                        |---|---|---|---|---|---|---|
                        | # | Date | Description | Chq/Ref. No. | Withdrawal (Dr.) | Deposit (Cr.) | Balance |
                        | - | - | Opening Balance | - | - | - | 1,966.12 |
                        | 1 | 01 Jun 2026 | Salary | IMPS-123 | | 15,000.00 | 16,966.12 |

                        | Account Summary | Account Summary | Account Summary |
                        |---|---|---|
                        | Particulars | Opening Balance | Closing Balance |
                        | Savings Account | 1,966.12 | 6,199.24 |
                        """;
                var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                {
                        document = new { json_content = (object?)null, md_content = markdown }
                });

                var transactions = new TransactionResultParser().Parse(body);

                var transaction = Assert.Single(transactions);
                Assert.Equal(1, transaction.SourceSequence);
                Assert.Equal("Salary", transaction.Description);
                Assert.Equal(TransactionDirection.Credit, transaction.Direction);
                Assert.Equal(15_000m, transaction.Amount);
                Assert.Equal(16_966.12m, transaction.BalanceAfter);
        }

            [Theory]
            [InlineData("output1.json", 368)]
            [InlineData("output2.txt", 51)]
            public void ParserMapsCompleteExtractionSamples(string fileName, int expectedTransactionCount)
            {
                var samplePath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory,
                    "..", "..", "..", "..", "ExpenseTracker", "Options", fileName));

                var transactions = new TransactionResultParser().Parse(File.ReadAllBytes(samplePath));

                Assert.Equal(expectedTransactionCount, transactions.Count);
            }

    [Fact]
    public async Task PdfPreparationOpensSupportedPasswordProtectedPdf()
    {
        await using var sourceStream = new MemoryStream();
        using (var document = new PdfDocument())
        {
            document.AddPage();
            document.SecuritySettings.UserPassword = "secret";
            document.Save(sourceStream, closeStream: false);
        }

        sourceStream.Position = 0;
        var service = new PdfPreparationService(
            Microsoft.Extensions.Options.Options.Create(new DocumentExtractionOptions()),
            Microsoft.Extensions.Options.Options.Create(new PdfDecryptionOptions { QpdfExecutablePath = ResolveQpdfExecutablePath() }));

        await using var preparedPdf = await service.PrepareAsync(sourceStream, "secret", CancellationToken.None);

        Assert.True(File.Exists(preparedPdf.Path));
    }

    [Fact]
    public async Task PdfPreparationCapturesWebHyperlinksInVisualOrder()
    {
        const string topLink = "https://example.test/top.pdf";
        const string bottomLink = "https://example.test/bottom.pdf";
        await using var sourceStream = new MemoryStream();
        using (var document = new PdfDocument())
        {
            var page = document.AddPage();
            page.AddWebLink(
                new PdfRectangle(
                    new PdfSharpCore.Drawing.XPoint(10, 100),
                    new PdfSharpCore.Drawing.XPoint(50, 80)),
                bottomLink);
            page.AddWebLink(
                new PdfRectangle(
                    new PdfSharpCore.Drawing.XPoint(10, 700),
                    new PdfSharpCore.Drawing.XPoint(50, 680)),
                topLink);
            document.Save(sourceStream, closeStream: false);
        }

        sourceStream.Position = 0;
        var service = new PdfPreparationService(
            Microsoft.Extensions.Options.Options.Create(new DocumentExtractionOptions()),
            Microsoft.Extensions.Options.Options.Create(new PdfDecryptionOptions()));

        await using var preparedPdf = await service.PrepareAsync(sourceStream, null, CancellationToken.None);

        Assert.Equal([topLink, bottomLink], preparedPdf.HyperlinksByPage[1]);
    }

    private static string ResolveQpdfExecutablePath()
    {
        var configured = Environment.GetEnvironmentVariable("QPDF_EXECUTABLE_PATH");
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (Directory.Exists(programFiles))
        {
            foreach (var directory in Directory.GetDirectories(programFiles, "qpdf*"))
            {
                var binDirectory = Path.Combine(directory, "bin");
                if (!Directory.Exists(binDirectory))
                {
                    continue;
                }

                var candidate = Path.Combine(binDirectory, "qpdf.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return "qpdf";
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