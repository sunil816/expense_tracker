using ExpenseTracker.Data;
using ExpenseTracker.Options;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ExpenseTracker");
if (string.IsNullOrWhiteSpace(connectionString))
{
	throw new InvalidOperationException("ConnectionStrings:ExpenseTracker is required.");
}

builder.Services.AddDbContextFactory<ExpenseTrackerDbContext>(options => options.UseNpgsql(connectionString));

var extractionSection = builder.Configuration.GetSection(DocumentExtractionOptions.SectionName);
builder.Services.AddOptions<DocumentExtractionOptions>()
	.Bind(extractionSection)
	.Configure(options => options.ConversionOptions = DocumentExtractionOptions.ReadConversionOptions(extractionSection.GetSection(nameof(DocumentExtractionOptions.ConversionOptions))))
	.ValidateDataAnnotations()
	.ValidateOnStart();

var extractionOptions = extractionSection.Get<DocumentExtractionOptions>() ?? new DocumentExtractionOptions();
extractionOptions.ConversionOptions = DocumentExtractionOptions.ReadConversionOptions(extractionSection.GetSection(nameof(DocumentExtractionOptions.ConversionOptions)));
const long multipartHeadroomBytes = 1024 * 1024;
var maximumMultipartBodySize = checked(extractionOptions.MaximumFileSizeBytes + multipartHeadroomBytes);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maximumMultipartBodySize);

builder.Services.AddOptions<PdfDecryptionOptions>()
	.Bind(builder.Configuration.GetSection(PdfDecryptionOptions.SectionName))
	.ValidateDataAnnotations()
	.ValidateOnStart();
builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = maximumMultipartBodySize;
	options.ValueLengthLimit = checked((int)Math.Min(extractionOptions.MaximumFileSizeBytes, int.MaxValue));
});

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
	options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("document-extraction", client =>
{
	client.BaseAddress = new Uri(extractionOptions.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(extractionOptions.HttpTimeoutSeconds);
});
builder.Services.AddSingleton<IPdfPreparationService, PdfPreparationService>();
builder.Services.AddSingleton<IDocumentExtractionProvider, DoclingDocumentExtractionProvider>();
builder.Services.AddSingleton<TransactionResultParser>();
builder.Services.AddScoped<DocumentImportService>();
builder.Services.AddScoped<ITransactionDuplicateService, TransactionDuplicateService>();
builder.Services.AddScoped<DocumentExtractionService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new SemaphoreSlim(extractionOptions.MaximumConcurrentExtractions));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
	await using var scope = app.Services.CreateAsyncScope();
	var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ExpenseTrackerDbContext>>();
	await using var context = await contextFactory.CreateDbContextAsync();
	await context.Database.MigrateAsync();

	app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// More specific than the SPA fallback below, so unknown API routes keep returning ProblemDetails.
app.MapFallback("/api/{**rest}", () => Results.Problem(
	statusCode: StatusCodes.Status404NotFound,
	title: "Resource not found."));
app.MapFallbackToFile("index.html");

app.Run();
