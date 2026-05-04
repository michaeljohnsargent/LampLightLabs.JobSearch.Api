using CsvHelper;
using System.Globalization;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Reads CSV files and returns an enumerable of dictionaries representing each row.
    /// </summary>
    public interface ICsvReaderService
    {
        /// <summary>
        /// Reads a CSV file from the specified relative path and returns an enumerable of dictionaries, where each dictionary
        /// represents a row in the CSV file, with column names as keys and cell values as values.
        /// </summary>
        /// <param name="relativePath">The relative path to the CSV file.</param>
        /// <returns>An enumerable of dictionaries representing each row in the CSV file.</returns>
        IEnumerable<IDictionary<string, string>> ReadCsv(string relativePath);
    }

    /// <summary>
    /// Reader service that uses CsvHelper to read CSV files. It reads the header row to 
    /// determine column names and then yields dictionaries for each subsequent row, 
    /// mapping column names to their respective values.
    /// </summary>
    public class CsvReaderService : ICsvReaderService
    {
        // We inject IWebHostEnvironment in case we need to resolve file paths relative to the web root or content root.
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Reader service constructor that accepts the web hosting environment for potential file path resolution.
        /// </summary>
        /// <param name="env">The web hosting environment.</param>
        public CsvReaderService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Reads a CSV file from the specified path and returns an enumerable of dictionaries, where each dictionary
        /// represents a row in the CSV file, with column names as keys and cell values as values.
        /// </summary>
        /// <param name="filePath">The path to the CSV file.</param>
        /// <returns>An enumerable of dictionaries representing each row in the CSV file.</returns>
        public IEnumerable<IDictionary<string, string>> ReadCsv(string filePath)
        {
            using var sr = new StreamReader(filePath);
            using var csv = new CsvReader(sr, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            while (csv.Read())
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    dict[header] = csv.GetField(header) ?? string.Empty;
                }
                yield return dict;
            }
        }
    }
}