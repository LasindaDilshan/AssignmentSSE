using Assignmane.Entities;
using System.Collections.Concurrent;

namespace Assignmane.Repository
{
    public class ChatSessionRepository
    {
        private readonly ConcurrentDictionary<Guid, ChatSession> _sessions = new ConcurrentDictionary<Guid, ChatSession>();

        public void Add(ChatSession session)
        {
            _sessions.TryAdd(session.Id, session);
        }

        public ChatSession Get(Guid id)
        {
            _sessions.TryGetValue(id, out var session);
            return session;
        }

        public void Update(ChatSession session)
        {
            _sessions[session.Id] = session;
        }

        public IEnumerable<ChatSession> GetAll()
        {
            return _sessions.Values;
        }
    }
}
