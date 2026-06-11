using Xunit;
using Processor.Internal.Domain;
using Aggregator.Internal.Services;

namespace DeveloperMetrics.Tests
{
    public class EventProcessorServiceTests
    {
        private readonly EventProcessorService _service;

        public EventProcessorServiceTests()
        {
            _service = new EventProcessorService();
        }

        [Fact]
        public void Validate_Should_Return_True_When_Event_Is_Valid()
        {
            // Arrange
            var rawEvent = new RawEvent { DeveloperId = "jean-dev", MetricType = "commit" };

            // Act
            var (isValid, _) = _service.Validate(rawEvent);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("", "commit")] // DeveloperId vazio
        [InlineData("jean-dev", "")] // MetricType vazio
        public void Validate_Should_Return_False_When_Required_Fields_Are_Missing(string devId, string metric)
        {
            // Arrange
            var rawEvent = new RawEvent { DeveloperId = devId, MetricType = metric };

            // Act
            var (isValid, _) = _service.Validate(rawEvent);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void Transform_Should_Map_Values_Correctly()
        {
            // Arrange
            var rawEvent = new RawEvent { DeveloperId = "jean-dev", Value = 10 };
            var instanceId = "test-instance";

            // Act
            var processed = _service.Transform(rawEvent, instanceId);

            // Assert
            Assert.Equal("jean-dev", processed.DeveloperId);
            Assert.Equal(10, processed.Value);
            Assert.Equal("test-instance", processed.ProcessorId);
        }

        [Theory]
        [InlineData("commit", "total_commits")]
        [InlineData("pull_request", "total_pull_requests")]
        public void Aggregator_Should_Map_To_Correct_Category(string metricType, string expectedCategory)
        {
            // Arrange
            var ev = new ProcessedEvent { MetricType = metricType };

            // Act
            // Se você ainda não extraiu a lógica de agregação para um serviço, 
            // faremos isso agora para garantir que esse teste passe.
            string category = AggregatorLogic.DetermineCategory(ev.MetricType);

            // Assert
            Assert.Equal(expectedCategory, category);
        }
    }
}