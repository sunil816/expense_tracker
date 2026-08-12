using System.Globalization;
using System.Text.Json;
using ExpenseTracker.Models.Extraction;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Services;

public sealed class TransactionResultParser
{
    private static readonly string[] PaymentHeaders = ["Name", "Bank", "Amount", "Date", "Status"];
    private static readonly string[] StatementHeaders = ["#", "Date", "Description", "Chq/Ref. No.", "Withdrawal (Dr.)", "Deposit (Cr.)", "Balance"];
    private static readonly string[] OrderHeaders = ["Date / Time", "Order ID", "Pod Name", "Amount", "View"];

    public IReadOnlyList<ExtractedTransaction> Parse(byte[] body) =>
        Parse(body, new Dictionary<int, IReadOnlyList<string>>());

    public IReadOnlyList<ExtractedTransaction> Parse(
        byte[] body,
        IReadOnlyDictionary<int, IReadOnlyList<string>> hyperlinksByPage)
    {
        try
        {
            using var result = JsonDocument.Parse(body);
            if (!result.RootElement.TryGetProperty("document", out var document) || document.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var transactions = ParseStructuredTables(document, hyperlinksByPage);
            if (transactions.Count > 0)
            {
                return transactions;
            }

            return document.TryGetProperty("md_content", out var markdown) && markdown.ValueKind == JsonValueKind.String
                ? ParseMarkdownTables(markdown.GetString()!)
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<ExtractedTransaction> ParseStructuredTables(
        JsonElement document,
        IReadOnlyDictionary<int, IReadOnlyList<string>> hyperlinksByPage)
    {
        var transactions = new List<ExtractedTransaction>();
        if (!document.TryGetProperty("json_content", out var jsonContent)
            || jsonContent.ValueKind != JsonValueKind.Object
            || !jsonContent.TryGetProperty("tables", out var tables)
            || tables.ValueKind != JsonValueKind.Array)
        {
            return transactions;
        }

        var textRowsByPage = ParseStructuredTextRows(jsonContent);
        var processedTextPages = new HashSet<int>();
        var hyperlinkIndexesByPage = new Dictionary<int, int>();
        string[]? headers = null;
        foreach (var table in tables.EnumerateArray())
        {
            if (TryGetPageNumber(table, out var pageNumber)
                && processedTextPages.Add(pageNumber)
                && textRowsByPage.TryGetValue(pageNumber, out var textRows))
            {
                foreach (var textRow in textRows)
                {
                    AddMappedRow(transactions, textRow.Headers, textRow.Values);
                }
            }

            if (!table.TryGetProperty("data", out var data)
                || !data.TryGetProperty("grid", out var grid)
                || grid.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var row in grid.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var values = row.EnumerateArray().Select(GetCellText).ToArray();
                if (TryRecognizeHeaders(values, out var recognizedHeaders))
                {
                    headers = recognizedHeaders;
                    continue;
                }

                if (headers is not null && IsTransactionRow(headers, values))
                {
                    ApplyOrderHyperlink(headers, values, pageNumber, hyperlinksByPage, hyperlinkIndexesByPage);
                    AddMappedRow(transactions, headers, values);
                }
            }
        }

        foreach (var pageNumber in textRowsByPage.Keys.Except(processedTextPages).Order())
        {
            foreach (var textRow in textRowsByPage[pageNumber])
            {
                AddMappedRow(transactions, textRow.Headers, textRow.Values);
            }
        }

        return transactions;
    }

    private static void ApplyOrderHyperlink(
        string[] headers,
        string[] values,
        int pageNumber,
        IReadOnlyDictionary<int, IReadOnlyList<string>> hyperlinksByPage,
        Dictionary<int, int> hyperlinkIndexesByPage)
    {
        if (!ReferenceEquals(headers, OrderHeaders)
            || !hyperlinksByPage.TryGetValue(pageNumber, out var hyperlinks))
        {
            return;
        }

        var hyperlinkIndex = hyperlinkIndexesByPage.GetValueOrDefault(pageNumber);
        if (hyperlinkIndex < hyperlinks.Count)
        {
            values[4] = hyperlinks[hyperlinkIndex];
            hyperlinkIndexesByPage[pageNumber] = hyperlinkIndex + 1;
        }
    }

    private static Dictionary<int, List<(string[] Headers, string[] Values)>> ParseStructuredTextRows(JsonElement jsonContent)
    {
        var rowsByPage = new Dictionary<int, List<(string[] Headers, string[] Values)>>();
        if (!jsonContent.TryGetProperty("texts", out var texts) || texts.ValueKind != JsonValueKind.Array)
        {
            return rowsByPage;
        }

        var fragments = new List<(int Page, double Top, double Left, string Text)>();
        foreach (var text in texts.EnumerateArray())
        {
            if (!text.TryGetProperty("label", out var label)
                || label.GetString() != "page_header"
                || !text.TryGetProperty("text", out var value)
                || value.ValueKind != JsonValueKind.String
                || !TryGetPageNumber(text, out var pageNumber)
                || !text.TryGetProperty("prov", out var provenance)
                || provenance.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var position = provenance.EnumerateArray().FirstOrDefault();
            if (position.ValueKind != JsonValueKind.Object
                || !position.TryGetProperty("bbox", out var boundingBox)
                || !boundingBox.TryGetProperty("t", out var top)
                || !boundingBox.TryGetProperty("l", out var left))
            {
                continue;
            }

            fragments.Add((pageNumber, Math.Round(top.GetDouble(), 2), left.GetDouble(), value.GetString()!.Trim()));
        }

        foreach (var group in fragments.GroupBy(fragment => (fragment.Page, fragment.Top)).OrderBy(group => group.Key.Page).ThenByDescending(group => group.Key.Top))
        {
            var values = group.OrderBy(fragment => fragment.Left).Select(fragment => fragment.Text).ToArray();
            var headers = values.Length switch
            {
                5 when IsTransactionRow(PaymentHeaders, values) => PaymentHeaders,
                7 when IsTransactionRow(StatementHeaders, values) => StatementHeaders,
                _ => null
            };
            if (headers is null)
            {
                continue;
            }

            if (!rowsByPage.TryGetValue(group.Key.Page, out var rows))
            {
                rows = [];
                rowsByPage[group.Key.Page] = rows;
            }

            rows.Add((headers, values));
        }

        return rowsByPage;
    }

    private static bool TryGetPageNumber(JsonElement element, out int pageNumber)
    {
        pageNumber = 0;
        if (!element.TryGetProperty("prov", out var provenance) || provenance.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var position = provenance.EnumerateArray().FirstOrDefault();
        return position.ValueKind == JsonValueKind.Object
            && position.TryGetProperty("page_no", out var page)
            && page.TryGetInt32(out pageNumber);
    }

    private static List<ExtractedTransaction> ParseMarkdownTables(string markdown)
    {
        var transactions = new List<ExtractedTransaction>();
        string[]? headers = null;

        foreach (var line in markdown.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith('|'))
            {
                headers = null;
                continue;
            }

            var values = trimmedLine.Trim('|').Split('|').Select(value => value.Trim()).ToArray();
            if (TryRecognizeHeaders(values, out var recognizedHeaders))
            {
                headers = recognizedHeaders;
                continue;
            }

            if (headers is not null && IsTransactionRow(headers, values))
            {
                AddMappedRow(transactions, headers, values);
            }
        }

        return transactions;
    }

    private static string GetCellText(JsonElement cell) =>
        cell.ValueKind == JsonValueKind.Object
        && cell.TryGetProperty("text", out var text)
        && text.ValueKind == JsonValueKind.String
            ? text.GetString()!.Trim()
            : string.Empty;

    private static bool TryRecognizeHeaders(string[] values, out string[] headers)
    {
        if (Matches(values, PaymentHeaders))
        {
            headers = PaymentHeaders;
            return true;
        }

        if (Matches(values, StatementHeaders))
        {
            headers = StatementHeaders;
            return true;
        }

        if (Matches(values, OrderHeaders))
        {
            headers = OrderHeaders;
            return true;
        }

        headers = [];
        return false;
    }

    private static bool Matches(string[] values, string[] headers) =>
        values.Length == headers.Length
        && values.Select((value, index) => value.Equals(headers[index], StringComparison.OrdinalIgnoreCase)).All(matches => matches);

    private static bool IsTransactionRow(string[] headers, string[] values) =>
        TryMapRow(headers, values, out _);

    private static void AddMappedRow(List<ExtractedTransaction> transactions, string[] headers, string[] values)
    {
        if (TryMapRow(headers, values, out var transaction))
        {
            transactions.Add(transaction);
        }
    }

    private static bool TryMapRow(string[] headers, string[] values, out ExtractedTransaction transaction)
    {
        transaction = null!;
        if (values.Length != headers.Length)
        {
            return false;
        }

        if (ReferenceEquals(headers, PaymentHeaders))
        {
            if (!values[4].Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(values[0])
                || !decimal.TryParse(values[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var signedAmount)
                || !DateOnly.TryParseExact(values[3], "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return false;
            }

            transaction = new(
                null,
                SourceDocumentFormat.PaymentExport,
                date,
                values[0].Trim(),
                NullIfWhiteSpace(values[1]),
                null,
                signedAmount < 0 ? TransactionDirection.Debit : TransactionDirection.Credit,
                Math.Abs(signedAmount),
                null,
                null);
            return true;
        }

        if (ReferenceEquals(headers, StatementHeaders))
        {
            if (!int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                || !DateOnly.TryParseExact(values[1], "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || string.IsNullOrWhiteSpace(values[2])
                || !TryParseOptionalAmount(values[4], out var withdrawal)
                || !TryParseOptionalAmount(values[5], out var deposit)
                || withdrawal.HasValue == deposit.HasValue
                || !decimal.TryParse(values[6], NumberStyles.Number, CultureInfo.InvariantCulture, out var balance))
            {
                return false;
            }

            transaction = new(
                sequence,
                SourceDocumentFormat.BankStatement,
                date,
                values[2].Trim(),
                null,
                NullIfWhiteSpace(values[3]),
                withdrawal.HasValue ? TransactionDirection.Debit : TransactionDirection.Credit,
                Math.Abs(withdrawal ?? deposit!.Value),
                balance,
                null);
            return true;
        }

        if (ReferenceEquals(headers, OrderHeaders)
            && DateOnly.TryParseExact(values[0], "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var orderDate)
            && long.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out _)
            && !string.IsNullOrWhiteSpace(values[2])
            && decimal.TryParse(values[3].TrimStart('₹'), NumberStyles.Number, CultureInfo.InvariantCulture, out var orderAmount))
        {
            transaction = new(
                null,
                SourceDocumentFormat.OrderHistory,
                orderDate,
                values[2].Trim(),
                null,
                values[1],
                TransactionDirection.Debit,
                Math.Abs(orderAmount),
                null,
                TryGetHttpUrl(values[4]));
            return true;
        }

        return false;
    }

    private static bool TryParseOptionalAmount(string value, out decimal? amount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            amount = null;
            return true;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount))
        {
            amount = parsedAmount;
            return true;
        }

        amount = null;
        return false;
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TryGetHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;
}