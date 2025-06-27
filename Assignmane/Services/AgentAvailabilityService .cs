using Assignmane.Entities;
using Assignmane.Enums;
using Assignmane.Services.Interfaces;

namespace Assignmane.Services
{
    public class AgentAvailabilityService : IAgentAvailabilityService
    {
        private readonly List<Team> _teams;
        private readonly Team _overflowTeam;

        public AgentAvailabilityService()
        {
            _teams = InitializeTeams(); // 🔧 Move this logic outside for test injection
            _overflowTeam = InitializeOverflowTeam();
        }

        public bool TryGetAvailableAgent(out Agent availableAgent)
        {
            var activeTeam = GetActiveTeam();

            var allAvailableAgents = activeTeam.Agents
                                               .Where(a => a.CanHandleMoreChats())
                                               .ToList();

            int capacity = (int)Math.Floor(
                activeTeam.Agents.Sum(a => 10 * GetEfficiency(a.Seniority))
            );
            int maxQueueLength = (int)(capacity * 1.5);

            if (allAvailableAgents.Count == 0 && IsOfficeHours())
            {
                allAvailableAgents.AddRange(
                    _overflowTeam.Agents.Where(a => a.CanHandleMoreChats())
                );
            }

            if (allAvailableAgents.Any())
            {
                var sortedAgents = allAvailableAgents.OrderBy(a => a.Seniority).ToList();
                availableAgent = sortedAgents.First();
                return true;
            }

            availableAgent = null;
            return false;
        }

        public IEnumerable<Agent> GetRegularAgents() => _teams.SelectMany(t => t.Agents);

        public IEnumerable<Agent> GetAllAgentsIncludingOverflow()
        {
            return IsOfficeHours()
                ? GetRegularAgents().Concat(_overflowTeam.Agents)
                : GetRegularAgents();
        }

        // 🔧 Logic assumes static time blocks per team — make dynamic if needed
        private Team GetActiveTeam()
        {
            var hour = DateTime.UtcNow.Hour;
            if (hour >= 8 && hour < 16) return _teams[0];
            if (hour >= 16 && hour < 24) return _teams[1];
            return _teams[2];
        }

        // 🔧 Time logic should be injected for better testing (IClock or DateTimeProvider) - Time Zone Needed
        private bool IsOfficeHours()
        {
            var hour = DateTime.UtcNow.Hour;
            return hour >= 9 && hour < 17;
        }

        private double GetEfficiency(Seniority seniority) => seniority switch
        {
            Seniority.Junior => 0.4,
            Seniority.MidLevel => 0.6,
            Seniority.Senior => 0.8,
            Seniority.TeamLead => 0.5,
            _ => 0.0
        };

        // ❌ Hardcoded data — should ideally come from configuration or a seed database
        private List<Team> InitializeTeams()
        {
            return new List<Team>
            {
                new Team
                {
                    Name = "Team A",
                    Agents = new List<Agent>
                    {
                        new Agent("Alice", Seniority.TeamLead),
                        new Agent("Bob", Seniority.MidLevel),
                        new Agent("Charlie", Seniority.MidLevel),
                        new Agent("Diana", Seniority.Junior)
                    }
                },
                new Team
                {
                    Name = "Team B",
                    Agents = new List<Agent>
                    {
                        new Agent("Eve", Seniority.Senior),
                        new Agent("Frank", Seniority.MidLevel),
                        new Agent("Grace", Seniority.Junior),
                        new Agent("Heidi", Seniority.Junior)
                    }
                },
                new Team
                {
                    Name = "Team C",
                    Agents = new List<Agent>
                    {
                        new Agent("Ivan", Seniority.MidLevel),
                        new Agent("Judy", Seniority.MidLevel)
                    }
                }
            };
        }

        public List<Team> Teams => _teams;
        public Team OverflowTeam => _overflowTeam;

        private Team InitializeOverflowTeam()
        {
            var overflowTeam = new Team { Name = "Overflow" };
            for (int i = 0; i < 6; i++)
            {
                overflowTeam.Agents.Add(new Agent($"Overflow {i + 1}", Seniority.Junior));
            }
            return overflowTeam;
        }
    }
}
