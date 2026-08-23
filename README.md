# Event-Driven Multi-Agent Platform (Pure Disconnected Architecture & Hybrid Home/Office AI Switcher)

A production-ready, modular, event-driven Multi-Agent System targeting **.NET 10 (LTS) with C# 14** for core orchestration, micro-agents, and interactive Blazor dashboard, combined with **Python (`pywin32` / COM)** for local Outlook desktop automation and dynamic script execution.

---

## 🏛️ Pure Disconnected System Architecture & Hybrid AI Switcher

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
  │                                                                   │
  ├──► [ Hybrid AI Provider Factory (AiProviderFactory) ]             │
  │     ├── Home Mode: Google Gemini API & Google Document AI         │
  │     └── Office Mode: Azure AI Foundry & Azure Document Intel      │
  │                                                                   │
[ SqlDataAgent Worker (.NET 10) ] ────── (Polls & Updates AgentTasks)  │
                                                                      │
[ Interactive Blazor Server Dashboard (.NET 10) ] ────────────────────┘
  (Direct SQL Server DbContext Connection)
```

---

## 🏡 Home vs. 🏢 Office AI Provider Switching

Easily switch AI providers for LLM completions and Document Intelligence between **Home (Google Cloud / Gemini)** and **Office (Azure Enterprise)** using configuration key `"AiSettings:Environment"`:

### 1. Home Mode (`"AiSettings:Environment": "Home"`)
- **LLM Completions**: Google Gemini API (`GoogleGeminiLlmProvider` using `gemini-2.5-flash`).
- **Document Intelligence**: Google Document AI & Gemini Multimodal (`GoogleDocumentAiProvider`).

### 2. Office Mode (`"AiSettings:Environment": "Office"`)
- **LLM Completions**: Azure AI Foundry / Azure OpenAI (`AzureAiFoundryLlmProvider`).
- **Document Intelligence**: Azure Document Intelligence / Form Recognizer (`AzureDocumentIntelligenceProvider`).

```json
{
  "AiSettings": {
    "Environment": "Home",
    "Home": {
      "GoogleApiKey": "YOUR_GEMINI_API_KEY",
      "ModelName": "gemini-2.5-flash"
    },
    "Office": {
      "AzureAiFoundryEndpoint": "https://your-foundry-endpoint.services.ai.azure.com/models",
      "AzureAiFoundryApiKey": "YOUR_AZURE_KEY",
      "AzureDocIntelEndpoint": "https://your-doc-intel.cognitiveservices.azure.com/",
      "AzureDocIntelApiKey": "YOUR_DOC_INTEL_KEY"
    }
  }
}
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
  - `AiProviderFactory.cs`: Dynamically resolves `GoogleGeminiLlmProvider` vs. `AzureAiFoundryLlmProvider` and `GoogleDocumentAiProvider` vs. `AzureDocumentIntelligenceProvider`.
  - `OrchestratorQueueWorker.cs`: `BackgroundService` polling `InboundEvents` where `Status = 'Pending'`.

### 2. `src/AgentDashboard` (.NET 10 Blazor Server Web App)
- **Role**: Interactive management dashboard connected directly to SQL Server.

### 3. `src/Agents/SqlDataAgent` (.NET 10 Worker)
- **Role**: SQL query execution micro-agent.

### 4. `src/Agents/OutlookEmailAgent` (Python Desktop Agent)
- **Role**: Local desktop Outlook bridge (**Zero Graph API & Zero SMTP**).

### 5. `src/Common/Contracts` (.NET 10 Class Library)
- **Key Models**: `DatabaseQueueModels.cs`, `AiProviderModels.cs` (`LlmCompletionRequest`, `DocumentAnalysisRequest`).

### 6. `tests/CentralOrchestrator.Tests` (.NET 10 XUnit Test Suite)
- Passed **12 / 12 automated unit tests**.

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
