using Assignmane.Entities;
using System.Collections.Concurrent;
using System.Reflection;

namespace AssignmentUnitTest
{
    public class UnitTest1
    {
        [Fact]
        public async Task ProcessQueue_AssignsQueuedSessionToAgentQueue()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session = new ChatSession
            {
                Id = sessionId,
                Status = ChatStatus.Queued
            };

            var mockRepo = new Mock<ChatSessionRepository>();
            mockRepo.Setup(r => r.Get(sessionId)).Returns(session);

            var mockAgent = new Agent { Name = "Test Agent" };
            var mockAgentAvailability = new Mock<IAgentAvailabilityService>();
            mockAgentAvailability
                .Setup(a => a.TryGetAvailableAgent(out mockAgent))
                .Returns(true);

            var service = new AgentChatCoordinatorService(
                new Mock<RabbitMQService>().Object,
                mockRepo.Object,
                mockAgentAvailability.Object
            );

            var sessionQueueField = typeof(AgentChatCoordinatorService)
                .GetField("_sessionQueue", BindingFlags.NonPublic | BindingFlags.Instance);
            var sessionQueue = new ConcurrentQueue<Guid>();
            sessionQueue.Enqueue(sessionId);
            sessionQueueField!.SetValue(service, sessionQueue);

            // Act
            var token = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500)).Token;
            await service.GetType()
                .GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(service, new object[] { token });

            // Assert
            Assert.Single(mockAgent.AgentQueue);
        }

    }
}