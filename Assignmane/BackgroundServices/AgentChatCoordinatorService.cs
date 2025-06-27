using Assignmane.Entities;
using Assignmane.Enums;
using Assignmane.Queue.Services;
using Assignmane.Repository;
using Assignmane.Services.Interfaces;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;

namespace Assignmane.BackgroundServices
{
    public class AgentChatCoordinatorService : BackgroundService
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly ChatSessionRepository _sessionRepository;
        private readonly List<Team> _teams;
        private readonly Team _overflowTeam;
        private readonly ConcurrentQueue<Guid> _sessionQueue = new ConcurrentQueue<Guid>();
        private int _agentIndex = 0;
        private IAgentAvailabilityService _agentAvailabilityService;

        public AgentChatCoordinatorService(RabbitMQService rabbitMQService, ChatSessionRepository sessionRepository , IAgentAvailabilityService agentAvailabilityService)
        {
            _rabbitMQService = rabbitMQService;
            _sessionRepository = sessionRepository;
            _agentAvailabilityService = agentAvailabilityService;
            // Get the teams and overflow team from the service
            _teams = _agentAvailabilityService.Teams.ToList();
            _overflowTeam = _agentAvailabilityService.OverflowTeam;
        }


        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();


            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "admin",
                Password = "admin"
            };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: "SessionQueue", durable: true, exclusive: false, autoDelete: false,
                arguments: null);

            Console.WriteLine(" [*] Waiting for messages.");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received {message}");
                
                if (Guid.TryParse(JsonConvert.DeserializeObject<string>(message), out var sessionId))
                {
                    _sessionQueue.Enqueue(sessionId);
                }
                else
                {
                    Console.WriteLine($" [!] Invalid GUID format received: {message}");
                    // Optionally: log or move to dead-letter queue
                }
                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync("SessionQueue", autoAck: true, consumer: consumer);

            var queueProcessing = ProcessQueue(stoppingToken);  // Can use Background service for this
            var sessionMonitoring = MonitorInactiveSessions(stoppingToken);
            var shiftMonitoring = MonitorAgentShifts(stoppingToken);
            var agentQueueMonitor = MonitorAgentQueues(stoppingToken);

            await Task.WhenAll(queueProcessing, sessionMonitoring, shiftMonitoring, agentQueueMonitor);

        }


        private async Task ProcessQueue(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_sessionQueue.TryDequeue(out var sessionId))
                {
                    var session = _sessionRepository.Get(sessionId);
                    if (session == null || session.Status != ChatStatus.Queued) continue;

                    // Use shared service logic to get an available agent
                    if (_agentAvailabilityService.TryGetAvailableAgent(out var agentToAssign))
                    {
                        agentToAssign.AgentQueue.Enqueue(session.Id);

                        Console.WriteLine($"Queued Chat {session.Id} to Agent {agentToAssign.Name}'s queue.");
                    }

                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        private async Task MonitorAgentQueues(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"Working MonitorAgentQueues");

                    var allAgents = _agentAvailabilityService.GetAllAgentsIncludingOverflow(); // or from _teams
                    foreach (var agent in allAgents) // can be scaled by creating queue on RabbitMQ   
                    {
                        try
                        {
                            if (agent.CanHandleMoreChats() && agent.AgentQueue.TryDequeue(out var sessionId))
                            {
                                var session = _sessionRepository.Get(sessionId);
                                if (session == null || session.Status != ChatStatus.Queued)
                                {
                                    continue;
                                }

                                agent.ActiveChats.Add(session);

                                session.AssignedAgentId = agent.Id;
                                session.AssignmentTime = DateTime.UtcNow;
                                session.Status = ChatStatus.Assigned;
                                session.Agent = agent;
                                _sessionRepository.Update(session);

                                Console.WriteLine($"[Agent {agent.Name}] Chat {session.Id} assigned.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing agent {agent.Name} in MonitorAgentQueues: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in MonitorAgentQueues loop: {ex}");
                    // Optionally decide whether to break or continue
                    // break; or continue;
                }

                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when stoppingToken is canceled
                    break;
                }
            }
        }



        private async Task MonitorInactiveSessions(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var sessionsToCheck = _sessionRepository.GetAll()
                        .Where(s => s.Status == ChatStatus.Assigned || s.Status == ChatStatus.Queued);

                    Console.WriteLine($"Working MonitorInactiveSessions - Checking {sessionsToCheck.Count()} sessions.");

                    foreach (var session in sessionsToCheck)
                    {
                        try
                        {
                            var secondsSinceLastPoll = (DateTime.UtcNow - session.LastPollTime).TotalSeconds;
                            if (secondsSinceLastPoll > 200)
                            {
                                session.Status = ChatStatus.Inactive;
                                _sessionRepository.Update(session);

                                if (session.AssignedAgentId.HasValue)
                                {
                                    var agent = _teams.SelectMany(t => t.Agents).FirstOrDefault(a => a.Id == session.AssignedAgentId);
                                    if (agent != null)
                                    {
                                        agent.ActiveChats.RemoveAll(cs => cs.Id == session.Id);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Agent with ID {session.AssignedAgentId} not found when clearing inactive session {session.Id}.");
                                    }
                                }

                                Console.WriteLine($"Chat {session.Id} marked as inactive due to no polling.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing session {session.Id} in MonitorInactiveSessions: {ex}");
                            // optionally continue to next session
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in MonitorInactiveSessions loop: {ex}");
                    // Optionally decide if you want to break the loop or continue:
                    // break; or continue;
                }

                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // This exception is expected when the token is canceled during the delay.
                    break;
                }
            }
        }


        private async Task MonitorAgentShifts(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"Working MonitorAgentShifts");

                    var currentHour = DateTime.UtcNow.Hour;
                    var allAgents = _teams.SelectMany(t => t.Agents).ToList();

                    foreach (var agent in allAgents)
                    {
                        try
                        {
                            bool wasOnShift = agent.IsOnShift;
                            // Determine current shift status
                            agent.IsOnShift = GetAgentShiftStatus(agent, currentHour);

                            // If agent is now off shift but was on shift before
                            if (!agent.IsOnShift && wasOnShift && agent.ActiveChats.Any())
                            {
                                Console.WriteLine($"Agent {agent.Name}'s shift has ended. Disconnecting their active chats.");
                                var chatsToDisconnect = new List<ChatSession>(agent.ActiveChats);

                                foreach (var chat in chatsToDisconnect)
                                {
                                    try
                                    {
                                        var sessionInRepo = _sessionRepository.Get(chat.Id);
                                        if (sessionInRepo != null)
                                        {
                                            sessionInRepo.Status = ChatStatus.Disconnected;
                                            _sessionRepository.Update(sessionInRepo);
                                            Console.WriteLine($"Chat {chat.Id} has been disconnected.");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Session {chat.Id} not found in repository during disconnection.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error disconnecting chat {chat.Id}: {ex}");
                                    }
                                }

                                agent.ActiveChats.Clear();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing agent {agent.Name}: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in MonitorAgentShifts loop: {ex}");
                    // Optionally: decide whether to break the loop or continue
                    // break; or continue;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when the stoppingToken is cancelled
                    break;
                }
            }
        }


        private bool GetAgentShiftStatus(Agent agent, int currentHour)
        {
            if (_teams[0].Agents.Contains(agent)) return currentHour >= 8 && currentHour < 16; // Team A
            if (_teams[1].Agents.Contains(agent)) return currentHour >= 16; // Team B
            if (_teams[2].Agents.Contains(agent)) return currentHour >= 0 && currentHour < 8; // Team C
            return false;
        }

    }
}
