# Event-Driven Multi-Agent Platform (100% Pure Disconnected Architecture - Zero WebAPI)

A production-ready, modular, event-driven Multi-Agent System targeting **.NET 10 (LTS) with C# 14** for core orchestration, micro-agents, and interactive Blazor dashboard, combined with **Python (`pywin32` / COM)** for local Outlook desktop automation and dynamic script execution.

---

## 🏛️ Pure Disconnected System Architecture

```
[ Local Outlook Desktop App (MAPI) ]
▲                     │
│ (pywin32 COM Send)  │ (pywin32 COM Watcher)
│                     ▼
[ Python Local Email Agent (pywin32 + DB Worker) ]
▲                                   │
│ (Polls AgentTasks)                │ (Inserts InboundEvents)
▼                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│               MICROSOFT SQL SERVER (AgentRegistryDb)                   │
├─────────────────┬──────────────────┬─────────────────┬─────────────────┤
│  InboundEvents  │    AgentTasks    │   HumanTasks    │     Agents      │
│  (Event Bus)    │   (Work Queue)   │ (Action Items)  │ (Capabilities)  │
└─────────────────┴──────────────────┴─────────────────┴─────────────────┘
▲                                   ▲                                 ▲
│ (Polls InboundEvents)             │ (Creates & Monitors AgentTasks) │ (Direct DB Access)
▼                                   ▼                                 │
[ Central Orchestrator Worker (Pure .NET 10 Worker - Zero WebAPI) ]────┤
                                                                      │
[ SqlDataAgent Worker (.NET 10) ] ────── (Polls & Updates AgentTasks)  │
                                                                      │
[ Interactive Blazor Server Dashboard (.NET 10) ] ────────────────────┘
  (Direct SQL Server DbContext Connection)
```

---

## 🔒 Why Zero WebAPI Architecture?

1. **Zero Open Web Ports**: The Central Orchestrator and sub-agents run as background worker processes. They open **zero inbound HTTP ports**, providing maximum enterprise security.
2. **Direct DB Connectivity**: The Blazor Server Dashboard connects directly to SQL Server (`AgentRegistryDb`) via Entity Framework Core (`DbContext`).
3. **100% Resilience & Zero Overhead**: Eliminates Kestrel WebAPI server overhead, network timeouts, and port conflicts.

---

## 📦 Projects & Solution Breakdown

### 1. `src/CentralOrchestrator` (.NET 10 LTS Pure Worker Service)
- **Role**: Core orchestration engine & database event bus processor (no Kestrel/WebAPI).
- **Key Features**:
  - `Program.cs`: Built using `.NET Host.CreateDefaultBuilder()` as a pure Windows / Background Worker Service.
  - `OrchestratorQueueWorker.cs`: `BackgroundService` polling `InboundEvents` where `Status = 'Pending'`.
  - `ITaskPlanner`: Uses `Microsoft.Extensions.AI` (`IChatClient`) to dynamically match input prompts against registered agent capabilities in SQL Server and decompose events into child `AgentTasks`.

### 2. `src/AgentDashboard` (.NET 10 Blazor Server Web App)
- **Role**: Interactive management dashboard connected directly to SQL Server.
- **Key Pages**:
  - `TasksPage.razor`: View & manage Action Items via direct `IHumanTaskService` database operations.
  - `AgentsPage.razor`: Dynamic Agent Registry Manager via direct `IAgentRegistryService`.
  - `TriggerTaskPage.razor`: Enqueues prompts directly into `InboundEvents` via direct `IDatabaseQueueService`.

### 3. `src/Agents/SqlDataAgent` (.NET 10 Worker)
- **Role**: SQL query execution micro-agent.
- **Key Features**: `SqlQueueWorker.cs` background service polling `AgentTasks` where `TargetAgentId = 'sql-data-agent'`, executing queries safely, and returning Markdown table results into SQL Server.

### 4. `src/Agents/OutlookEmailAgent` (Python Desktop Agent)
- **Role**: Local desktop Outlook bridge (**Zero Graph API & Zero SMTP**).
- **Key Features**:
  - `main.py` inbox watcher inserting unread MAPI emails directly into `InboundEvents` table.
  - Outbound queue worker polling `AgentTasks` where `TargetAgentId = 'outlook-email-agent'`, executing `win32com.client` send/reply operations, and updating task status to `'Completed'`.

### 5. `src/Common/Contracts` (.NET 10 Class Library)
- **Key Models**: `DatabaseQueueModels.cs` (`InboundEventDto`, `AgentTaskDto`, `CreateInboundEventRequest`, `UpdateTaskResultRequest`).

### 6. `tests/CentralOrchestrator.Tests` (.NET 10 XUnit Test Suite)
- Passed **10 / 10 automated unit tests**.

---

## 🗄️ Database Schema (Microsoft SQL Server `AgentRegistryDb`)

1. **`InboundEvents`**: `Id` (PK), `EventGuid` (UNIQUE), `Source`, `Prompt`, `DataJson`, `Status`, `CreatedAt`, `ProcessedAt`.
2. **`AgentTasks`**: `Id` (PK), `TaskGuid` (UNIQUE), `ParentEventGuid` (FK), `StepOrder`, `TargetAgentId`, `Action`, `PayloadJson`, `Status`, `ResultJson`, `ErrorMessage`, `CreatedAt`, `StartedAt`, `CompletedAt`.
3. **`Agents`**: `Id` (PK), `Name`, `Description`, `TransportType`, `IsActive`, `RegisteredAt`, `LastHeartbeat`.
4. **`AgentCapabilities`**: `Id` (PK), `AgentId` (FK), `CapabilityName`, `Description`, `ParametersJsonSchema`.
5. **`HumanTasks`**: `Id` (PK), `EventId`, `Title`, `Description`, `AssignedAgentId`, `Status`, `Priority`, `CreatedAt`, `CompletedAt`.

---

## 🚀 How to Run

### 1. Build and Run Tests
```bash
dotnet build MultiAgentPlatform.sln
dotnet test MultiAgentPlatform.sln
```

### 2. Run Central Orchestrator Worker Service (Zero HTTP Ports)
```bash
dotnet run --project src/CentralOrchestrator/CentralOrchestrator.csproj
```

### 3. Run SqlDataAgent Worker
```bash
dotnet run --project src/Agents/SqlDataAgent/SqlDataAgent.csproj
```

### 4. Run Blazor Dashboard
```bash
dotnet run --project src/AgentDashboard/AgentDashboard.csproj
```

### 5. Run Python Outlook Agent
```bash
cd src/Agents/OutlookEmailAgent
pip install -r requirements.txt
python main.py
```
