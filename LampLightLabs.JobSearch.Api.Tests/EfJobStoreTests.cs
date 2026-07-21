using LampLightLabs.JobSearch.Api.Data;
using LampLightLabs.JobSearch.Api.Models;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LampLightLabs.JobSearch.Api.Tests
{
    /// <summary>
    /// Tests for EfJobStore — verifies EF Core-backed CRUD behavior using the
    /// InMemory provider (no real Postgres required for unit tests; the InMemory
    /// provider still exercises DbContext/DbSet/change-tracking behavior).
    /// Each test builds its own uniquely-named database via <see cref="NewContext"/>
    /// so tests don't share state.
    /// </summary>
    public class EfJobStoreTests
    {
        private static JobSearchDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<JobSearchDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new JobSearchDbContext(options);
        }

        [Fact]
        public async Task CreateJobAsync_ReturnsJobWithValidJobId()
        {
            // Arrange
            var store = new EfJobStore(NewContext());

            // Act
            var job = await store.CreateJobAsync();

            // Assert
            Assert.NotNull(job);
            Assert.NotEqual(string.Empty, job.JobId);
            Assert.Equal(JobStatus.Queued, job.Status);
            Assert.Null(job.Result);
            Assert.True(job.CreatedAt > DateTime.MinValue);
        }

        [Fact]
        public async Task GetJobAsync_ExistingJobId_ReturnsCorrectJob()
        {
            // Arrange
            var db = NewContext();
            var store = new EfJobStore(db);
            var createdJob = await store.CreateJobAsync();

            // Act
            var retrievedJob = await store.GetJobAsync(createdJob.JobId);

            // Assert
            Assert.NotNull(retrievedJob);
            Assert.Equal(createdJob.JobId, retrievedJob!.JobId);
            Assert.Equal(createdJob.Status, retrievedJob.Status);
            Assert.Equal(createdJob.Result, retrievedJob.Result);
        }

        [Fact]
        public async Task GetJobAsync_NonExistingJobId_ReturnsNull()
        {
            // Arrange
            var store = new EfJobStore(NewContext());

            // Act
            var retrievedJob = await store.GetJobAsync("non-existing-id");

            // Assert
            Assert.Null(retrievedJob);
        }

        [Fact]
        public async Task UpdateJobAsync_ExistingJobId_UpdatesStatus()
        {
            // Arrange
            var db = NewContext();
            var store = new EfJobStore(db);
            var job = await store.CreateJobAsync();

            // Act
            await store.UpdateJobAsync(job.JobId, j => j.Status = JobStatus.Processing);
            var updatedJob = await store.GetJobAsync(job.JobId);

            // Assert
            Assert.NotNull(updatedJob);
            Assert.Equal(JobStatus.Processing, updatedJob!.Status);
        }

        [Fact]
        public async Task UpdateJobAsync_NonExistingJobId_DoesNotThrow()
        {
            // Arrange
            var store = new EfJobStore(NewContext());

            // Act
            await store.UpdateJobAsync("non-existing-id", j => j.Status = JobStatus.Processing);
            var retrievedJob = await store.GetJobAsync("non-existing-id");

            // Assert
            Assert.Null(retrievedJob);
        }

        /// <summary>
        /// Strategy Pattern proof test: JobStore (in-memory) and EfJobStore (EF Core)
        /// both implement IJobStore. Given the same sequence of calls through the
        /// interface, both produce equivalent results — the caller (JobsController)
        /// does not need to know which implementation is in use. Same proof shape as
        /// CsvReaderServiceTests' StrategyPattern_CsvAndJsonReturnIdenticalResults.
        /// </summary>
        [Fact]
        public async Task StrategyPattern_InMemoryAndEfStoresBehaveIdentically()
        {
            // Arrange — both assigned to the interface, caller sees no difference
            IJobStore inMemoryStore = new JobStore();
            IJobStore efStore = new EfJobStore(NewContext());

            // Act
            var inMemoryJob = await inMemoryStore.CreateJobAsync();
            var efJob = await efStore.CreateJobAsync();

            await inMemoryStore.UpdateJobAsync(inMemoryJob.JobId, j =>
            {
                j.Status = JobStatus.Complete;
                j.Result = "Processed 3 applications successfully.";
            });
            await efStore.UpdateJobAsync(efJob.JobId, j =>
            {
                j.Status = JobStatus.Complete;
                j.Result = "Processed 3 applications successfully.";
            });

            var inMemoryResult = await inMemoryStore.GetJobAsync(inMemoryJob.JobId);
            var efResult = await efStore.GetJobAsync(efJob.JobId);

            // Assert — same shape and values, different JobIds (each generates its own)
            Assert.NotNull(inMemoryResult);
            Assert.NotNull(efResult);
            Assert.Equal(inMemoryResult!.Status, efResult!.Status);
            Assert.Equal(inMemoryResult.Result, efResult.Result);
        }
    }
}
