using Assignmane.Enums;

namespace Assignmane.Entities
{
    public class ChatSession
    {
        
        public Guid Id { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime? AssignmentTime { get; set; }
        public Guid? AssignedAgentId { get; set; }
        public DateTime LastPollTime { get; set; }
        public ChatStatus Status { get; set; }
        public Agent? Agent { get; set; }

    }
}
