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

    /// <summary>
    /// Tests for JsonReaderService — verifies that JSON files are parsed correctly
    /// and that the service satisfies the ICsvReaderService contract.
    /// </summary>
    public class JsonReaderServiceTests
    {
        private readonly string _testJsonPath;

        public JsonReaderServiceTests()
        {
            // Build path to a temp JSON file we control
            _testJsonPath = Path.Combine(Path.GetTempPath(), "test_applications.json");
        }

        private void WriteTempJson(string content)
        {
            File.WriteAllText(_testJsonPath, content);
        }

        [Fact]
        public void ReadCsv_ValidJsonFile_ReturnsCorrectRowCount()
        {
            // Arrange
            WriteTempJson(
                "[" +
                "{\"Company\":\"Acme Corp\",\"Role\":\"Developer\",\"Status\":\"Applied\"}," +
                "{\"Company\":\"Beacon Hill\",\"Role\":\"Senior Engineer\",\"Status\":\"Interviewing\"}" +
                "]"
            );

            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new JsonReaderService(mockEnv.Object);

            // Act
            var results = service.ReadCsv(_testJsonPath).ToList();

            // Assert
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public void ReadCsv_ValidJsonFile_ReturnsCorrectFieldValues()
        {
            // Arrange
            WriteTempJson(
                "[{\"Company\":\"Beacon Hill\",\"Role\":\"Senior Engineer\",\"Status\":\"Interviewing\"}]"
            );

            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new JsonReaderService(mockEnv.Object);

            // Act
            var results = service.ReadCsv(_testJsonPath).ToList();

            // Assert
            Assert.Equal("Beacon Hill", results[0]["Company"]);
            Assert.Equal("Senior Engineer", results[0]["Role"]);
            Assert.Equal("Interviewing", results[0]["Status"]);
        }

        [Fact]
        public void ReadCsv_EmptyJsonArray_ReturnsNoRows()
        {
            // Arrange
            WriteTempJson("[]");

            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new JsonReaderService(mockEnv.Object);

            // Act
            var results = service.ReadCsv(_testJsonPath).ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void ReadCsv_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var mockEnv = new Mock<IWebHostEnvironment>();
            var service = new JsonReaderService(mockEnv.Object);

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() =>
                service.ReadCsv("nonexistent_file.json").ToList()
            );
        }

        /// <summary>
        /// Strategy Pattern proof test: CsvReaderService and JsonReaderService both implement
        /// ICsvReaderService. Given the same data in different formats, both return identical results.
        /// The caller does not need to know which implementation is in use.
        /// </summary>
        [Fact]
        public void StrategyPattern_CsvAndJsonReturnIdenticalResults()
        {
            // Arrange — same data, two formats
            var csvPath = Path.Combine(Path.GetTempPath(), "strategy_test.csv");
            var jsonPath = Path.Combine(Path.GetTempPath(), "strategy_test.json");

            File.WriteAllText(csvPath,
                "Company,Role,Status\n" +
                "Acme Corp,Developer,Applied\n"
            );

            File.WriteAllText(jsonPath,
                "[{\"Company\":\"Acme Corp\",\"Role\":\"Developer\",\"Status\":\"Applied\"}]"
            );

            var mockEnv = new Mock<IWebHostEnvironment>();

            // Both assigned to the interface — caller sees no difference
            ICsvReaderService csvService = new CsvReaderService(mockEnv.Object);
            ICsvReaderService jsonService = new JsonReaderService(mockEnv.Object);

            // Act
            var csvResults = csvService.ReadCsv(csvPath).ToList();
            var jsonResults = jsonService.ReadCsv(jsonPath).ToList();

            // Assert — same row count, same field values
            Assert.Equal(csvResults.Count, jsonResults.Count);
            Assert.Equal(csvResults[0]["Company"], jsonResults[0]["Company"]);
            Assert.Equal(csvResults[0]["Role"], jsonResults[0]["Role"]);
            Assert.Equal(csvResults[0]["Status"], jsonResults[0]["Status"]);
        }
    }
}