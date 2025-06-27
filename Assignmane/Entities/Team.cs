namespace Assignmane.Entities
{
    public class Team
    {
        public string Name { get; set; }
        public List<Agent> Agents { get; set; } = new List<Agent>();

        public int TeamCapacity => Agents.Sum(a => a.MaxConcurrency);
        public int MaxQueueLength => (int)(TeamCapacity * 1.5);
    }
}
