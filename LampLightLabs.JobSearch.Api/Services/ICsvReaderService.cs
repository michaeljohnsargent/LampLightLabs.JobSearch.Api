namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Defines a contract for reading structured data files and returning each row
    /// as a dictionary of column name to value. Implementations may read from CSV,
    /// JSON, or other flat data sources — the caller does not need to know which.
    /// </summary>
    public interface ICsvReaderService
    {
        /// <summary>
        /// Reads a data file from the specified path and returns an enumerable of dictionaries,
        /// where each dictionary represents a row with column names as keys and cell values as values.
        /// </summary>
        /// <param name="filePath">The path to the data file to read.</param>
        /// <returns>An enumerable of dictionaries representing each row in the file.</returns>
        IEnumerable<IDictionary<string, string>> ReadCsv(string filePath);
    }
}
