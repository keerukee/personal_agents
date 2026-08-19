# Event-Driven Multi-Agent Platform (.NET 10 LTS & Local Outlook COM)

A production-ready, modular, event-driven Multi-Agent System targeting **.NET 10 (LTS) with C# 14** for core orchestration, micro-agents, and interactive Blazor dashboard, combined with **Python (`pywin32` / COM)** for local Outlook desktop automation and dynamic script execution.

---

## 🏛️ System Architecture Overview

```
[ Local Outlook Desktop App (MAPI) ]
▲                     │
│ (pywin32 COM Send)  │ (pywin32 COM Watcher)
│                     ▼
[ Python Local Email Agent (FastAPI + pywin32) ]
▲                     │
│ (POST /send)        │ (POST /api/events/inbound)
▼                     ▼
[ Central Orchestrator Agent (ASP.NET Core 10 Minimal API) ] ◄───► [ Microsoft SQL Server (AgentRegistryDb) ]
│                                     │                                ├── Agents
├─► [ Specialized Micro-Agents ]      ├─► [ Streamable HTTP MCP ]      ├── AgentCapabilities
│   - SqlDataAgent (.NET 10)          │   - SQL MCP Server             └── HumanTasks
│   - Dynamic Python Runner (FastAPI) │   - Custom Tools
│                                     │
└─► [ Interactive Blazor Server Dashboard (.NET 10) ]
    - Action Items & Human Task Board
    - Dynamic Agent Registry Manager
    - Trigger Event / Test Bench
```

---

## 📦 Projects & Solution Breakdown

The solution (`MultiAgentPlatform.sln`) is organized into decoupled, modular projects:

### 1. `src/CentralOrchestrator` (.NET 10 LTS WebAPI)
- **Role**: Core orchestration engine and dynamic routing hub.
- **Key Features**:
  - **Generic Ingestion Endpoint** (`POST /api/events/inbound`): Receives un-coupled event payloads (`AgentEventMessage`) from Python Outlook Agent or external triggers.
  - **Dynamic Agent Registry** (`IAgentRegistryService`): Persists active sub-agents and tool capability descriptions in **Microsoft SQL Server** (`AgentRegistryDb`).
  - **LLM Task Planner** (`ITaskPlanner`): Uses `Microsoft.Extensions.AI` (`IChatClient`) to dynamically match input prompts against registered sub-agent capability descriptions in SQL Server and generate DAG execution plans.
  - **Streamable HTTP MCP Client** (`IMcpHttpClient`): Executes remote or local tools using chunked JSON-RPC 2.0 streaming over HTTP POST (`POST /mcp`).
  - **Human Task Logging API** (`POST /api/tasks`, `GET /api/tasks`, `PUT /api/tasks/{id}/status`): Logs tasks requiring manual human review/action directly into SQL Server.

### 2. `src/AgentDashboard` (.NET 10 Blazor Server Web App)
- **Role**: Interactive management dashboard for human action items, dynamic agent management, and event testing.
- **Key Features**:
  - **Human Action Tasks Board** (`/tasks`): Visual board for viewing, reviewing, completing, and logging action items.
  - **Dynamic Agent Registry Manager** (`/agents`): UI to view registered agents, toggle active status, and register new sub-agents & capabilities dynamically into SQL Server.
  - **Trigger Test Bench** (`/trigger`): Form to dispatch test prompts and visualize real-time LLM DAG execution steps.

### 3. `src/Common/Contracts` (.NET 10 Class Library)
- **Role**: Shared data contracts and JSON-RPC 2.0 schemas.
- **Key Features**:
  - `GenericEventContracts.cs`: Fully decoupled event contract (`AgentEventMessage`). Zero sub-agent DTO compilation dependencies.
  - `TaskLogModels.cs`: DTOs for human task action items (`HumanTaskDto`, `CreateHumanTaskRequest`).
  - `McpContracts.cs`: JSON-RPC 2.0 specification models for Streamable HTTP MCP transport.
  - `AgentRegistryModels.cs`: Sub-agent registration and capability definitions.
  - `PlanExecutionModels.cs`: Execution plan and DAG step tracking DTOs.

### 4. `tests/CentralOrchestrator.Tests` (.NET 10 XUnit Test Suite)
- **Role**: Automated unit and integration testing.
- **Key Features**:
  - In-memory SQLite isolated tests for `AgentRegistryService`, `TaskPlannerService`, and `HumanTaskService`.
  - Passed 7 / 7 automated unit tests.

### 5. `src/Agents/OutlookEmailAgent` (Python FastAPI + `pywin32` COM)
- **Role**: Local desktop Outlook bridge.
- **Key Constraints**: **Zero Graph API & Zero SMTP**. Interacts directly with Microsoft Outlook desktop application via MAPI namespace.
- **Responsibilities**:
  - Polls inbox (`olFolderInbox = 6`), extracts attachments to local staging disk (`C:\temp\agent_staging`), deduplicates via local SQLite ledger (`EntryID`), and POSTs to Central Orchestrator.
  - Exposes `POST /api/email/send` and `POST /api/email/reply` using `win32com.client` wrapped with `pythoncom.CoInitialize()`.

### 6. `src/Agents/SqlDataAgent` (.NET 10 Streamable HTTP MCP Server)
- **Role**: SQL query execution micro-agent.
- **Responsibilities**:
  - Exposes `POST /mcp` Streamable HTTP MCP endpoint handling `tools/call` for `query_database`.
  - Executes parameterized read-only queries and formats results as Markdown/JSON tables.

### 7. `src/Agents/PythonRunnerAgent` (Python FastAPI)
- **Role**: Dynamic script execution agent.
- **Responsibilities**:
  - Endpoint `POST /execute-script`.
  - Inspects code AST for missing dependencies, automatically installs missing packages via `pip`, and executes code inside a sandboxed subprocess with timeout boundaries.

### 8. `src/SharedGateways` (.NET 10 Minimal API)
- **Role**: Shared AI service proxy.
- **Responsibilities**:
  - `POST /api/shared/doc-intelligence`: Document Intelligence text/tables extraction.
  - `POST /api/shared/ai-foundry`: Central proxy for Azure AI Foundry LLM completions.

---

## 🗄️ Database Schema (Microsoft SQL Server `AgentRegistryDb`)

EF Core 10 connects to SQL Server (`Server=localhost;Database=AgentRegistryDb;Trusted_Connection=True;TrustServerCertificate=True;`) and automatically manages 3 tables:

1. **`Agents`**: `Id` (PK), `Name`, `Description`, `EndpointUrl`, `TransportType`, `IsActive`, `RegisteredAt`, `LastHeartbeat`.
2. **`AgentCapabilities`**: `Id` (PK), `AgentId` (FK), `CapabilityName`, `Description`, `ParametersJsonSchema`.
3. **`HumanTasks`**: `Id` (PK), `EventId`, `Title`, `Description`, `AssignedAgentId`, `Status`, `Priority`, `CreatedAt`, `CompletedAt`.

---

## 🚀 Quick Start Instructions

### 1. Build and Test .NET Solution
```bash
dotnet build MultiAgentPlatform.sln
dotnet test MultiAgentPlatform.sln
```

### 2. Run Central Orchestrator WebAPI
```bash
dotnet run --project src/CentralOrchestrator/CentralOrchestrator.csproj
```
*(Automatically creates `AgentRegistryDb` on local SQL Server and seeds initial agents on startup)*

### 3. Run Agent Dashboard (Blazor Server)
```bash
dotnet run --project src/AgentDashboard/AgentDashboard.csproj
```
Open `http://localhost:5000` (or configured port) to access the interactive dashboard.

### 4. Run Python Outlook Agent
```bash
cd src/Agents/OutlookEmailAgent
pip install -r requirements.txt
python main.py
```

---

## 🌐 Office Network Deployment
To deploy to an office network SQL Server:
1. Update `ConnectionStrings:AgentRegistryConnection` in `src/CentralOrchestrator/appsettings.json` to point to your office SQL Server instance.
2. The application will automatically verify and create all required tables (`Agents`, `AgentCapabilities`, `HumanTasks`) on startup!
