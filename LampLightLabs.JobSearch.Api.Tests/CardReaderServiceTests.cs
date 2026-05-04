using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace LampLightLabs.JobSearch.Api.Tests
{
    public class CsvReaderServiceTests
    {
        private readonly string _testCsvPath;

        public CsvReaderServiceTests()
        {
            // Build path to a temp CSV file we control
            _testCsvPath = Path.Combine(Path.GetTempPath(), "test_applications.csv");
        }

        private void WriteTempCsv(string content)
        {
            File.WriteAllText(_testCsvPath, content);
        }

        [Fact]
        public void ReadCsv_ValidFile_ReturnsCorrectRowCount()
        {
            // Arrange
            WriteTempCsv(
                "Company,Role,Status\n" +
                "Acme Corp,Developer,Applied\n" +
                "Beacon Hill,Senior Engineer,Interviewing\n"
            );

            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new CsvReaderService(mockEnv.Object);

            // Act
            var results = service.ReadCsv(_testCsvPath).ToList();

            // Assert
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public void ReadCsv_ValidFile_ReturnsCorrectFieldValues()
        {
            // Arrange
            WriteTempCsv(
                "Company,Role,Status\n" +
                "Beacon Hill,Senior Engineer,Interviewing\n"
            );

            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new CsvReaderService(mockEnv.Object);

            // Act
            var results = service.ReadCsv(_testCsvPath).ToList();

            // Assert
            Assert.Equal("Beacon Hill", results[0]["Company"]);
            Assert.Equal("Senior Engineer", results[0]["Role"]);
            Assert.Equal("Interviewing", results[0]["Status"]);
        }

        [Fact]
        public void ReadCsv_EmptyFile_ReturnsNoRows()
        {
            // Arrange
            WriteTempCsv("Company,Role,Status\n");

            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new CsvReaderService(mockEnv.Object);

            // Act
            var results = service.ReadCsv(_testCsvPath).ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void ReadCsv_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new CsvReaderService(mockEnv.Object);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() =>
                service.ReadCsv("nonexistent_file.csv").ToList()
            );
        }
    }
}