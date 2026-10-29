using Microsoft.Extensions.Configuration;

namespace StockFlow.BusinessLogic;

public static class StockFlowConfiguration
{
    /// <summary>Loads optional local JSON from the executable directory, then prefixed environment overrides.</summary>
    public static IConfigurationRoot Load(string? baseDirectory = null)
    {
        try
        {
            return new ConfigurationBuilder()
                .SetBasePath(baseDirectory ?? AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "STOCKFLOW_")
                .Build();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or FormatException or UnauthorizedAccessException)
        {
            // JSON parser exceptions can contain the original document, including secrets.
            throw new InvalidOperationException(
                "Не вдалося прочитати appsettings.json. Перевірте формат JSON та доступ до локального файлу.");
        }
    }
}
