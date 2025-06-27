using Assignmane.Entities;

namespace Assignmane.Services.Interfaces
{
    public interface IAgentAvailabilityService
    {
        List<Team> Teams { get; }
        Team OverflowTeam { get; }
        bool TryGetAvailableAgent(out Agent availableAgent);
        IEnumerable<Agent> GetRegularAgents();
        IEnumerable<Agent> GetAllAgentsIncludingOverflow();

    }

}
