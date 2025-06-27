using Assignmane.Enums;
using System.Collections.Concurrent;

namespace Assignmane.Entities
{
    public class Agent
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Seniority Seniority { get; set; }
        public int MaxConcurrency { get; private set; }
        public List<ChatSession> ActiveChats { get; } = new List<ChatSession>();
        public bool IsOnShift { get; set; } = true;
        public ConcurrentQueue<Guid> AgentQueue { get; } = new(); // New queue per agent , can use rabbitmq

        public Agent(string name, Seniority seniority)
        {
            Id = Guid.NewGuid();
            Name = name;
            Seniority = seniority;
            SetMaxConcurrency();
        }

        private void SetMaxConcurrency()
        {
            const int baseConcurrency = 10;
            MaxConcurrency = (int)Math.Floor(baseConcurrency * GetEfficiency());
        }

        private double GetEfficiency()
        {
            return Seniority switch
            {
                Seniority.Junior => 0.4,
                Seniority.MidLevel => 0.6,
                Seniority.Senior => 0.8,
                Seniority.TeamLead => 0.5,
                _ => 0.0,
            };
        }

        public bool CanHandleMoreChats()
        {
            return ActiveChats.Count < MaxConcurrency && IsOnShift;
        }
    }
}
