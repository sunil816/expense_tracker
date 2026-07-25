using ExpenseTracker.Options;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

var extractionSection = builder.Configuration.GetSection(DocumentExtractionOptions.SectionName);
builder.Services.AddOptions<DocumentExtractionOptions>()
	.Bind(extractionSection)
	.ValidateDataAnnotations()
	.ValidateOnStart();

var extractionOptions = extractionSection.Get<DocumentExtractionOptions>() ?? new DocumentExtractionOptions();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = extractionOptions.MaximumFileSizeBytes);
builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = extractionOptions.MaximumFileSizeBytes;
	options.ValueLengthLimit = checked((int)Math.Min(extractionOptions.MaximumFileSizeBytes, int.MaxValue));
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("document-extraction", client =>
{
	client.BaseAddress = new Uri(extractionOptions.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(extractionOptions.HttpTimeoutSeconds);
});
builder.Services.AddSingleton<IPdfPreparationService, PdfPreparationService>();
builder.Services.AddSingleton<IDocumentExtractionProvider, DoclingDocumentExtractionProvider>();
builder.Services.AddSingleton(new SemaphoreSlim(extractionOptions.MaximumConcurrentExtractions));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
