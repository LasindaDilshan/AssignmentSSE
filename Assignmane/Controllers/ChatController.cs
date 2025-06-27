using Assignmane.Entities;
using Assignmane.Enums;
using Assignmane.Queue.Services;
using Assignmane.Repository;
using Assignmane.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Assignmane.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly ChatSessionRepository _sessionRepository;
        private readonly IAgentAvailabilityService _agentAvailability;


        public ChatController(RabbitMQService rabbitMQService, ChatSessionRepository sessionRepository , IAgentAvailabilityService agentAvailabilityService)
        {
            _rabbitMQService = rabbitMQService;
            _sessionRepository = sessionRepository;
            _agentAvailability = agentAvailabilityService;
        }

        /// <summary>
        /// Creates a new chat session and adds it to the queue.
        /// </summary>
        [HttpPost("CreateSession")]
        public IActionResult CreateSession()
        {

            try
            {
                if (_agentAvailability.TryGetAvailableAgent(out var agent))
                {
                    var chatSession = new ChatSession
                    {
                        Id = Guid.NewGuid(),
                        RequestTime = DateTime.UtcNow,
                        Status = ChatStatus.Queued,
                        LastPollTime = DateTime.UtcNow
                    };

                    _sessionRepository.Add(chatSession);
                    _rabbitMQService.PublishMessage("SessionQueue", chatSession.Id); // Send only the ID

                    return Ok(new { status = "available", Message = "Chat session created. Start polling.", SessionId = chatSession.Id });
                }
                else
                {
                    return Ok(new { status = "busy", message = "All agents are currently busy." });

                }

                
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, "An error occurred while creating the chat session.");
            }
        }

        /// <summary>
        /// Allows the chat window to poll for a session's status.
        /// </summary>
        /// <param name="sessionId">The ID of the chat session.</param>
        [HttpPost("Poll")]
        public IActionResult Poll([FromBody] Guid sessionId)
        {
            try
            {
                var session = _sessionRepository.Get(sessionId);
                if (session == null)
                {
                    return NotFound(new { status = "not_found", message = "Session not found or has expired." });
                }

                session.LastPollTime = DateTime.UtcNow;
                _sessionRepository.Update(session);

                if (session.Status == ChatStatus.Queued)
                {
                    return Ok(new
                    {
                        status = "pending",
                        message = "Your session is in queue. Please wait..."
                    });
                }

                if (session.Status == ChatStatus.Assigned)
                {
                    string agentName = session.Agent.Name ?? "";

                    return Ok(new
                    {
                        status = "assigned",
                        message = "You have been connected to an agent.",
                        agent = agentName
                    });
                }

                return Ok(new
                {
                    status = session.Status.ToString().ToLower(),
                    message = "Session status updated."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = $"Unexpected error: {ex.Message}"
                });
            }
        }


    }
}
