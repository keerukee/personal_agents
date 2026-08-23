# Event-Driven Multi-Agent Platform (Pure Disconnected Architecture, MySQL Lab Reports & Hybrid AI Switcher)

A production-ready, modular, event-driven Multi-Agent System targeting **.NET 10 (LTS) with C# 14** for core orchestration, micro-agents, and interactive Blazor dashboard, combined with **Python (`pywin32` / COM)** for local Outlook desktop automation and dynamic script execution.

---

## 🏛️ Pure Disconnected System Architecture

```
[ Gmail / External Sender ]
          │ (Email to keerukee@outlook.com)
          ▼
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
  │                                                                   │
  ├──► [ Hybrid AI Provider Factory (AiProviderFactory) ]             │
  │     ├── Home Mode: Google Gemini API & Google Document AI         │
  │     └── Office Mode: Azure AI Foundry & Azure Document Intel      │
  │                                                                   │
├─► [ MySqlDataAgent Worker (.NET 10) ] ── (Queries MySQL 3306 labreports)
├─► [ SqlDataAgent Worker (.NET 10) ] ──── (Polls & Updates AgentTasks)
│                                                                     │
└─► [ Interactive Blazor Server Dashboard (.NET 10) ] ────────────────┘
  (Direct SQL Server DbContext Connection)
```

---

## 🏥 MySQL `labreports` Database Integration

The solution includes a dedicated **MySqlDataAgent** (`src/Agents/MySqlDataAgent`):
- **Target Database**: MySQL running on `localhost:3306` (user: `root`, password: `root`, database: `labreports`).
- **Capability**: `query_labreports_db` (fetches last N patient records, lab test results, and medical reports).
- **Execution**: Runs fully disconnected as a background worker polling `AgentTasks` where `TargetAgentId = 'mysql-data-agent'`.

---

## 📩 End-to-End Email to MySQL AI Response Flow

1. **Trigger Email**: Send an email from Gmail to `keerukee@outlook.com` asking: *"Please provide last 5 patients information AI response"*.
2. **Inbox Ingestion**: Python Outlook Agent (`main.py`) picks up the email from desktop Outlook MAPI and enqueues an event into SQL Server `InboundEvents`.
3. **Orchestration**: Central Orchestrator Worker (`OrchestratorQueueWorker`) decomposes the prompt into two child tasks:
   - **Step 1**: `mysql-data-agent` -> Action `query_labreports_db` (fetches last 5 patient records from MySQL `localhost:3306`).
   - **Step 2**: `outlook-email-agent` -> Action `send_reply` (replies to the email with the formatted Markdown patient report).
4. **Execution & Automated Reply**: `MySqlDataAgent` extracts the patient data from MySQL, and `OutlookEmailAgent` sends the response email via desktop Outlook MAPI!

---

## 🏡 Home vs. 🏢 Office AI Provider Switching

Easily switch AI providers for LLM completions and Document Intelligence between **Home (Google Cloud / Gemini)** and **Office (Azure Enterprise)** using configuration key `"AiSettings:Environment"`:

### 1. Home Mode (`"AiSettings:Environment": "Home"`)
- **LLM Completions**: Google Gemini API (`GoogleGeminiLlmProvider` using `gemini-2.5-flash`).
- **Document Intelligence**: Google Document AI & Gemini Multimodal (`GoogleDocumentAiProvider`).

### 2. Office Mode (`"AiSettings:Environment": "Office"`)
- **LLM Completions**: Azure AI Foundry / Azure OpenAI (`AzureAiFoundryLlmProvider`).
- **Document Intelligence**: Azure Document Intelligence / Form Recognizer (`AzureDocumentIntelligenceProvider`).

---

## 🚀 How to Run the Complete Platform

### 1. Build and Run Tests
```bash
dotnet build MultiAgentPlatform.sln
dotnet test MultiAgentPlatform.sln
```

### 2. Run Central Orchestrator Worker Service (Zero HTTP Ports)
```bash
dotnet run --project src/CentralOrchestrator/CentralOrchestrator.csproj
```

### 3. Run MySqlDataAgent Worker (MySQL localhost:3306)
```bash
dotnet run --project src/Agents/MySqlDataAgent/MySqlDataAgent.csproj
```

### 4. Run Blazor Dashboard
```bash
dotnet run --project src/AgentDashboard/AgentDashboard.csproj
```

### 5. Run Python Outlook Desktop Ingestion & Reply Worker
```bash
cd src/Agents/OutlookEmailAgent
pip install -r requirements.txt
python main.py
```
