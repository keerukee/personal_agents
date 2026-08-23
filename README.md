# Event-Driven Multi-Agent Platform (100% Disconnected Database-Driven Architecture)

A production-ready, modular, event-driven Multi-Agent System targeting **.NET 10 (LTS) with C# 14** for core orchestration, micro-agents, and interactive Blazor dashboard, combined with **Python (`pywin32` / COM)** for local Outlook desktop automation and dynamic script execution.

---

## 🏛️ System Architecture Overview

```
[ Local Outlook Desktop App (MAPI) ]
▲                     │
│ (pywin32 COM Send)  │ (pywin32 COM Watcher)
│                     ▼
[ Python Local Email Agent (pywin32 + DB Queue Worker) ]
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
│ (Polls InboundEvents)             │ (Creates & Monitors AgentTasks) │
▼                                   ▼                                 │
[ Central Orchestrator Worker (OrchestratorQueueWorker .NET 10) ]───────┘
│
├─► [ SqlDataAgent Worker (.NET 10) ] ── (Polls & Updates AgentTasks)
├─► [ Dynamic Python Runner Worker ] ─── (Polls & Updates AgentTasks)
└─► [ Interactive Blazor Server Dashboard (.NET 10) ]
```

---

## 🔌 Disconnected Database Architecture Key Benefits

1. **Zero Direct Network RPCs Between Agents**: Sub-agents and microservices no longer expose HTTP endpoints to each other. Communication happens exclusively by reading and writing SQL Server database tables (`InboundEvents` and `AgentTasks`).
2. **Enterprise Firewall Friendly**: Agents require zero inbound HTTP port openings on corporate networks. Only standard outbound database access to SQL Server is needed.
3. **100% Fault-Tolerant Queue**: If any agent or central orchestrator is restarted or temporarily offline, all events and tasks wait safely in SQL Server without data loss.

---

## 📦 Projects & Solution Breakdown

### 1. `src/CentralOrchestrator` (.NET 10 LTS WebAPI & Queue Worker)
- **Role**: Core orchestration engine & database event bus processor.
- **Key Features**:
  - `OrchestratorQueueWorker.cs`: `BackgroundService` polling `InboundEvents` where `Status = 'Pending'`.
  - `ITaskPlanner`: Uses `Microsoft.Extensions.AI` (`IChatClient`) to dynamically match input prompts against registered agent capabilities in SQL Server and decompose events into child `AgentTasks`.
  - `DatabaseQueueService.cs`: Enqueues events, manages task claiming, and tracks event completion.

### 2. `src/AgentDashboard` (.NET 10 Blazor Server Web App)
- **Role**: Interactive management dashboard.
- **Key Pages**:
  - `TasksPage.razor`: View & manage Action Items and DB Task Queue status in real time.
  - `AgentsPage.razor`: Dynamic Agent Registry Manager.
  - `TriggerTaskPage.razor`: Test bench to enqueue prompts into `InboundEvents` and observe live DAG progression.

### 3. `src/Agents/SqlDataAgent` (.NET 10 Worker)
- **Role**: SQL query execution micro-agent.
- **Key Features**: `SqlQueueWorker.cs` background service polling `AgentTasks` where `TargetAgentId = 'sql-data-agent'`, executing queries safely, and returning Markdown/JSON table results to SQL Server.

### 4. `src/Agents/OutlookEmailAgent` (Python Desktop Agent)
- **Role**: Local desktop Outlook bridge (**Zero Graph API & Zero SMTP**).
- **Key Features**:
  - `main.py` inbox watcher inserting unread MAPI emails into `InboundEvents` table.
  - Outbound queue worker polling `AgentTasks` where `TargetAgentId = 'outlook-email-agent'`, executing `win32com.client` send/reply operations, and updating task status to `'Completed'`.

### 5. `src/Common/Contracts` (.NET 10 Class Library)
- **Key Models**: `DatabaseQueueModels.cs` (`InboundEventDto`, `AgentTaskDto`, `CreateInboundEventRequest`, `UpdateTaskResultRequest`). Zero sub-agent DTO coupling.

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

### 2. Run Central Orchestrator & Queue Worker
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
