using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Services;

public interface IPdfPreparationService
{
    Task<PreparedPdf> PrepareAsync(IFormFile file, string? password, CancellationToken cancellationToken);
}

public sealed class PreparedPdf : IAsyncDisposable
{
    public PreparedPdf(string path, string stagingDirectory)
    {
        Path = path;
        StagingDirectory = stagingDirectory;
    }

    public string Path { get; }

    private string StagingDirectory { get; }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(StagingDirectory))
            {
                Directory.Delete(StagingDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }
}