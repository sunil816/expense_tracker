using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Options;

public sealed class PdfDecryptionOptions : IValidatableObject
{
    public const string SectionName = "PdfDecryption";

    [Required]
    public string QpdfExecutablePath { get; set; } = "qpdf";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; set; } = 60;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(QpdfExecutablePath))
        {
            yield return new ValidationResult("QpdfExecutablePath is required.", [nameof(QpdfExecutablePath)]);
        }
    }
}
