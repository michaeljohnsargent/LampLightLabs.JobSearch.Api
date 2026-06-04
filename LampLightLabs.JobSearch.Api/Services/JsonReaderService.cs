using System.Text.Json;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Reads JSON files and returns each element as a dictionary of column name to value.
    /// Expects the JSON file to be an array of flat objects, where each object represents
    /// a row and its properties map directly to column names.
    /// Implements <see cref="ICsvReaderService"/> as an alternative to <see cref="CsvReaderService"/>,
    /// demonstrating the Strategy Pattern — the caller swaps implementations without changing behavior.
    /// </summary>
    public class JsonReaderService : ICsvReaderService
    {
        // IWebHostEnvironment is injected for potential future use resolving paths
        // relative to the web root or content root.
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Initializes a new instance of <see cref="JsonReaderService"/> with the
        /// web hosting environment for file path resolution.
        /// </summary>
        /// <param name="env">The web hosting environment.</param>
        public JsonReaderService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Reads a JSON file from the specified path and yields each element as a dictionary,
        /// mapping property names to their string values.
        /// </summary>
        /// <param name="filePath">The path to the JSON file. Expected format: a JSON array of flat objects.</param>
        /// <returns>An enumerable of dictionaries representing each element in the array.</returns>
        public IEnumerable<IDictionary<string, string>> ReadCsv(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);
            foreach (var row in rows ?? [])
                yield return row;
        }
    }
}
