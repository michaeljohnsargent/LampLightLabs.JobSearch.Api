using LampLightLabs.JobSearch.Api.Models;
using LampLightLabs.JobSearch.Api.Services;

namespace LampLightLabs.JobSearch.Api.Tests
{
    public class JobStoreTests
    {
        [Fact]
        public void CreateJob_ReturnsJobWithValidJobId()
        {
            // Arrange
            JobStore jobStore = new JobStore();

            // Act
            var job = jobStore.CreateJob();

            // Assert
            Assert.NotNull(job);
            Assert.NotEqual(string.Empty, job.JobId);
            Assert.Equal(JobStatus.Queued, job.Status);
            Assert.Null(job.Result);
            Assert.True(job.CreatedAt > DateTime.MinValue);
        }

        [Fact]
        public void GetJob_ExistingJobId_ReturnsCorrectJob()
        {
            // Arrange
            JobStore jobStore = new JobStore();
            var createdJob = jobStore.CreateJob();
            // Act
            var retrievedJob = jobStore.GetJob(createdJob.JobId);
            // Assert
            Assert.NotNull(retrievedJob);
            Assert.Equal(createdJob.JobId, retrievedJob.JobId);
            Assert.Equal(createdJob.Status, retrievedJob.Status);
            Assert.Equal(createdJob.Result, retrievedJob.Result);
        }

        [Fact]
        public void GetJob_NonExistingJobId_ReturnsNull()
        {
            // Arrange
            JobStore jobStore = new JobStore();
            // Act
            var retrievedJob = jobStore.GetJob("non-existing-id");
            // Assert
            Assert.Null(retrievedJob);
        }

        [Fact]
        public void UpdateJob_ExistingJobId_UpdateStatus()
        {
            // Arrange
            JobStore jobStore = new JobStore();
            var job = jobStore.CreateJob();
            // Act
            jobStore.UpdateJob(job.JobId, j => j.Status = JobStatus.Processing);
            var updatedJob = jobStore.GetJob(job.JobId);
            // Assert
            Assert.NotNull(updatedJob);
            Assert.Equal(JobStatus.Processing, updatedJob.Status);
        }

        [Fact]
        public void UpdateJob_NonExistingJobId_DoesNotThrow()
        {
            // Arrange
            JobStore jobStore = new JobStore();
            // Act
            jobStore.UpdateJob("non-existing-id", j => j.Status = JobStatus.Processing);
            var retrievedJob = jobStore.GetJob("non-existing-id");
            // Assert
            Assert.Null(retrievedJob);
        }
    }
}