using CsvHelper;
using System.Globalization;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Reads CSV files and returns each row as a dictionary of column name to value.
    /// Uses CsvHelper to parse the header row and map subsequent rows accordingly.
    /// Implements <see cref="ICsvReaderService"/> as the default production reader.
    /// </summary>
    public class CsvReaderService : ICsvReaderService
    {
        // IWebHostEnvironment is injected for potential future use resolving paths
        // relative to the web root or content root.
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Initializes a new instance of <see cref="CsvReaderService"/> with the
        /// web hosting environment for file path resolution.
        /// </summary>
        /// <param name="env">The web hosting environment.</param>
        public CsvReaderService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Reads a CSV file from the specified path and yields each row as a dictionary,
        /// mapping column headers to their respective cell values.
        /// </summary>
        /// <param name="filePath">The path to the CSV file.</param>
        /// <returns>An enumerable of dictionaries representing each data row.</returns>
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
