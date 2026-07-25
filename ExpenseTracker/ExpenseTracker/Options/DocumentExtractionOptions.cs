using System.ComponentModel.DataAnnotations;
using System.Text.Json;

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

        if (ConversionOptions.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            yield return new ValidationResult("ConversionOptions must be a JSON object or null.", [nameof(ConversionOptions)]);
        }
    }
}