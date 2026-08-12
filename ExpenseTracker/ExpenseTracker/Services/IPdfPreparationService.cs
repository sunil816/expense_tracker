namespace ExpenseTracker.Services;

public interface IPdfPreparationService
{
    Task<PreparedPdf> PrepareAsync(Stream input, string? password, CancellationToken cancellationToken);
}

public sealed class PreparedPdf : IAsyncDisposable
{
    public PreparedPdf(
        string path,
        string stagingDirectory,
        IReadOnlyDictionary<int, IReadOnlyList<string>> hyperlinksByPage)
    {
        Path = path;
        StagingDirectory = stagingDirectory;
        HyperlinksByPage = hyperlinksByPage;
    }

    public string Path { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<string>> HyperlinksByPage { get; }

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