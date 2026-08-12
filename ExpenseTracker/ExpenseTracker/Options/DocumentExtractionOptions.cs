using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExpenseTracker.Options;

public sealed class DocumentExtractionOptions : IValidatableObject
{
    public const string SectionName = "DocumentExtraction";

    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string SubmitPath { get; set; } = string.Empty;

    [Required]
    public string StatusPathTemplate { get; set; } = string.Empty;

    [Required]
    public string ResultPathTemplate { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int HttpTimeoutSeconds { get; set; } = 120;

    [Range(1, int.MaxValue)]
    public int ExtractionDeadlineSeconds { get; set; } = 900;

    [Range(1, int.MaxValue)]
    public int PollIntervalSeconds { get; set; } = 2;

    [Range(0, 20)]
    public int PollRetryAttempts { get; set; } = 3;

    [Range(0, 60000)]
    public int PollRetryBackoffMilliseconds { get; set; } = 250;

    [Range(1, long.MaxValue)]
    public long MaximumFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    [Range(1, long.MaxValue)]
    public long MaximumResponseSizeBytes { get; set; } = 50 * 1024 * 1024;

    [Required]
    public string FileFieldName { get; set; } = "files";

    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public JsonElement ConversionOptions { get; set; } = JsonDocument.Parse("{}").RootElement.Clone();

    [Range(1, int.MaxValue)]
    public int MaximumConcurrentExtractions { get; set; } = 2;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp)
        {
            yield return new ValidationResult("BaseUrl must be an absolute HTTP or HTTPS URL.", [nameof(BaseUrl)]);
        }

        if (StatusPathTemplate.Contains("{task_id}", StringComparison.OrdinalIgnoreCase) is false || ResultPathTemplate.Contains("{task_id}", StringComparison.OrdinalIgnoreCase) is false)
        {
            yield return new ValidationResult("StatusPathTemplate and ResultPathTemplate must contain {task_id}.", [nameof(StatusPathTemplate), nameof(ResultPathTemplate)]);
        }

        if (MaximumResponseSizeBytes < MaximumFileSizeBytes)
        {
            yield return new ValidationResult("MaximumResponseSizeBytes must be at least MaximumFileSizeBytes.", [nameof(MaximumResponseSizeBytes)]);
        }
    }

    public static JsonElement ReadConversionOptions(IConfigurationSection section)
    {
        var node = ReadNode(section);
        return JsonDocument.Parse(node?.ToJsonString() ?? "{}").RootElement.Clone();
    }

    private static JsonNode? ReadNode(IConfigurationSection section)
        {
            var children = section.GetChildren().ToArray();
            if (children.Length == 0)
            {
                return ParseScalar(section.Value);
            }

            var isArray = children.All(child => int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _));
            if (isArray)
            {
                var array = new JsonArray();
                foreach (var child in children.OrderBy(child => int.Parse(child.Key, CultureInfo.InvariantCulture)))
                {
                    array.Add(ReadNode(child));
                }

                return array;
            }

            var objectNode = new JsonObject();
            foreach (var child in children)
            {
                objectNode[child.Key] = ReadNode(child);
            }

            return objectNode;
        }

    private static JsonNode? ParseScalar(string? value)
        {
            if (value is null)
            {
                return null;
            }

            if (bool.TryParse(value, out var boolean))
            {
                return JsonValue.Create(boolean);
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return JsonValue.Create(integer);
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                return JsonValue.Create(number);
            }

            return JsonValue.Create(value);
        }

}