using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LampLightLabs.JobSearch.Api.Tests
{
    public class RaceConditionDemoTests
    {
        [Fact]
        public void Counter_WithoutLock_ProducesUnpredictableResults()
        {
            // Arrange
            int counter = 0;
            int interations = 1_000_000;

            // Act

            Thread thread1 = new Thread(() =>
            {
                for (int i = 0; i < interations; i++)
                {
                    counter++;
                }
            });

            Thread thread2 = new Thread(() =>
            {
                for (int i = 0; i < interations; i++)
                {
                    counter++;
                }
            });

            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();

            // Assert
            Assert.Equal(2_000_000, counter);
        }

        [Fact]
        public void Counter_WithLock_ProducesCorrectResult()
        {
            // Arrange
            int counter = 0;
            int interations = 1_000_000;
            object lockObject = new object();

            // Act

            Thread thread1 = new Thread(() =>
            {
                for (int i = 0; i < interations; i++)
                {
                    lock (lockObject)
                    {
                        counter++;
                    }
                }
            });

            Thread thread2 = new Thread(() =>
            {
                for (int i = 0; i < interations; i++)
                {
                    lock (lockObject)
                    {
                        counter++;
                    }
                }
            });

            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();

            // Assert
            Assert.Equal(2_000_000, counter);

        }
    }
}
