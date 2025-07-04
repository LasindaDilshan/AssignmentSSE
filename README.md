# Assignmane


### Chat Session Management System

## Getting Started

### Prerequisites
- .NET 6 SDK
- RabbitMQ server (running locally)
- Modern web browser
  
**Key Components Implemented:**

1. **Session Creation API (`CreateSession`):**
   - Checks agent availability using `_agentAvailabilityService`
   - Creates chat session with `Queued` status
   - Publishes session ID to RabbitMQ queue
   - Returns:
     - `available`: Session created (client should start polling)
     - `busy`: All agents occupied

2. **Polling API (`Poll`):**
   - Updates last poll time (prevent session expiration)
   - Returns session status:
     - `pending`: In queue
     - `assigned`: Connected to agent (includes agent name)
     - `inactive`: No polling for 3s
     - `not_found`: Invalid session ID

3. **Background Services:**
   - **RabbitMQ Consumer:** Processes new session IDs
   - **Queue Processing:** Assigns sessions to agents
   - **Agent Queue Monitor:** Assigns chats when agents have capacity
   - **Session Monitor:** Marks inactive sessions (3s no poll)
   - **Shift Monitor:** Handles shift changes and disconnects chats

---

### Business Logic Implementation

**Agent Assignment Flow:**
1. New sessions enter FIFO RabbitMQ queue
2. Background service dequeues sessions
3. System assigns to first available agent using round-robin
4. Session moves to agent's personal queue
5. When agent has capacity:
   - Session status changes to `Assigned`
   - Agent details sent to client on next poll

**Capacity Management:**
```python
# Capacity Calculation Example
(2 Mid x 10 x 0.6) + (1 Junior x 10 x 0.4) = 16 concurrent chats
Max Queue = 16 x 1.5 = 24
```
- Agents handle max 10 chats scaled by seniority:
  - Junior: 0.4 → 4 concurrent
  - Mid: 0.6 → 6 concurrent
  - Senior: 0.8 → 8 concurrent
  - Team Lead: 0.5 → 5 concurrent

**Shift Handling:**
- Teams work 8-hour shifts:
  - Team A: 00-08 UTC
  - Team B: 08-16 UTC 
  - Team C: 16-24 UTC
- Agents finish current chats when shift ends
- No new chats assigned to off-shift agents

**Overflow Mechanism:**
- 6 junior-equivalent agents
- Activated during office hours when main queue full
- Same capacity rules as regular teams

**Inactivity Handling:**
- Sessions marked `Inactive` after 3s without polling
- Agent slots freed immediately

---

### Key Technical Notes

Technical Approach:

In-memory ConcurrentQueue for session management

Centralized background service coordinating all tasks

Round-robin agent assignment

Local repository for session tracking


1. **Message Queue:** 
   - RabbitMQ used for decoupled session processing
   - Only session ID stored in messages (lightweight)

2. **Concurrency Control:**
   - Thread-safe `ConcurrentQueue` for session management
   - Atomic operations on agent work queues

3. **Error Handling:**
   - Try-catch blocks in all critical paths
   - Detailed console logging for diagnostics
   - Graceful 500 errors in APIs

4. **Data Flow:**
   ```mermaid
   graph LR
   Client-->CreateSession-->RabbitMQ
   RabbitMQ-->BackgroundService
   BackgroundService-->AgentQueue
   AgentQueue-->Poll[Poll API]
   Poll-->Client
   ```

5. **Team Configuration:**
   - **Team A:** 1 Lead, 2 Mid, 1 Junior
   - **Team B:** 1 Senior, 1 Mid, 2 Juniors
   - **Team C:** 2 Mid (night shift)
   - **Overflow:** 6 Junior-level

---

### Usage Example
```bash
# Start chat session
POST /CreateSession → Returns {sessionId: "xxx"}

# Poll for status (every 1s)
POST /Poll {sessionId}
→ {status: "pending", message: "In queue..."}
→ {status: "assigned", agent: "John"}
```


### Future Enhancements for Scalability

#### 1. 🚀 Decoupled Background Services  
**Current Limitation**:  
All monitoring tasks run within a single coordinator service:  
```csharp
var sessionMonitoring = MonitorInactiveSessions(stoppingToken);
var shiftMonitoring = MonitorAgentShifts(stoppingToken);
var agentQueueMonitor = MonitorAgentQueues(stoppingToken);
```  

**Improvement Plan**:  
- Implement specialized cloud-native services:  
  - `SessionInactivityService`: Dedicated to session timeout handling  
  - `ShiftRotationService`: Isolated shift management  
  - `AgentAssignmentService`: Exclusive chat assignment processor  

**Benefits**:  
- Independent scaling of each function  
- Fault isolation between components  
- Resource optimization based on workload  

---

#### 2. 🐇 RabbitMQ-Based Agent Queues  
**Current Limitation**:  
In-memory concurrent queues limit horizontal scaling:  
```csharp
public ConcurrentQueue<Guid> AgentQueue { get; } = new();
```  

**Improvement Plan**:  
Replace with dedicated RabbitMQ queues per agent:  
```csharp
// Queue declaration
channel.QueueDeclare($"agent-{agentId}-queue", durable: true);

// Message publishing
_rabbitMQ.Publish($"agent-{agentId}-queue", sessionId);
```  

**Benefits**:  
- Persistent queues survive service restarts  
- Distributed processing across multiple instances  
- Built-in dead-letter handling for failed assignments  
- Per-agent throughput monitoring  

---
