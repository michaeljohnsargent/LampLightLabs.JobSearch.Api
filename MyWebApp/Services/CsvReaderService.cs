using CsvHelper;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;
using System.Text;

namespace LampLightLabs.JobSearch.Api.Services
{
    public interface ICsvReaderService
    {
        IEnumerable<IDictionary<string, string>> ReadCsv(string relativePath);
    }

    public class CsvReaderService : ICsvReaderService
    {
        private readonly IWebHostEnvironment _env;

        public CsvReaderService(IWebHostEnvironment env)
        {
            _env = env;
        }

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