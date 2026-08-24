using CentralOrchestrator.Services.AI;
using Common.Contracts;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;

namespace CentralOrchestrator.Services;

public class TaskPlannerService : ITaskPlanner
{
    private readonly IAgentRegistryService _agentRegistry;
    private readonly IMcpHttpClient _mcpClient;
    private readonly HttpClient _httpClient;
    private readonly IAiProviderFactory? _aiProviderFactory;
    private readonly IChatClient? _chatClient;
    private readonly ILogger<TaskPlannerService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TaskPlannerService(
        IAgentRegistryService agentRegistry,
        IMcpHttpClient mcpClient,
        HttpClient httpClient,
        ILogger<TaskPlannerService> logger,
        IAiProviderFactory? aiProviderFactory = null,
        IChatClient? chatClient = null)
    {
        _agentRegistry = agentRegistry;
        _mcpClient = mcpClient;
        _httpClient = httpClient;
        _aiProviderFactory = aiProviderFactory;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<TaskPlan> CreatePlanAsync(AgentEventMessage eventMessage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating generic LLM-based execution plan for Event '{EventId}' (Source: {Source})", eventMessage.EventId, eventMessage.Source);

        // 1. Retrieve all active registered agents & capabilities dynamically from SQLite DB
        var activeAgents = await _agentRegistry.GetAllActiveAgentsAsync();

        if (activeAgents.Count == 0)
        {
            _logger.LogWarning("No active agents found in registry database.");
            return new TaskPlan(
                PlanId: $"plan_{Guid.NewGuid():N}",
                EventId: eventMessage.EventId,
                CreatedAt: DateTimeOffset.UtcNow,
                Steps: new List<TaskStep>(),
                Status: "NoAgentsRegistered"
            );
        }

        // 2. Format agent registry details into LLM context prompt
        var agentContextBuilder = new StringBuilder();
        foreach (var agent in activeAgents)
        {
            agentContextBuilder.AppendLine($"- Agent ID: '{agent.Id}', Name: '{agent.Name}', Transport: '{agent.TransportType}', Endpoint: '{agent.EndpointUrl}'");
            agentContextBuilder.AppendLine($"  Description: {agent.Description}");
            agentContextBuilder.AppendLine("  Capabilities:");
            foreach (var cap in agent.Capabilities)
            {
                agentContextBuilder.AppendLine($"    * Action: '{cap.CapabilityName}' - {cap.Description} (Schema: {cap.ParametersJsonSchema})");
            }
        }

        List<TaskStep> plannedSteps;

        // 3. Use active LLM Provider via IAiProviderFactory or IChatClient, otherwise perform dynamic capability matching
        var llmProvider = _aiProviderFactory?.GetLlmProvider();
        if (llmProvider != null)
        {
            _logger.LogInformation("Using active LLM Provider '{ProviderName}' ({Environment}) to evaluate event and generate DAG task plan", 
                llmProvider.ProviderName, _aiProviderFactory?.ActiveEnvironment);
            
            try
            {
                plannedSteps = await PlanWithLlmProviderAsync(eventMessage, agentContextBuilder.ToString(), activeAgents, llmProvider, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM provider planning failed. Falling back to dynamic capability matching.");
                plannedSteps = PlanWithCapabilityMatching(eventMessage, activeAgents);
            }
        }
        else if (_chatClient != null)
        {
            plannedSteps = await PlanWithLlmAsync(eventMessage, agentContextBuilder.ToString(), activeAgents, cancellationToken);
        }
        else
        {
            _logger.LogInformation("No active LLM ChatClient/Provider found. Performing dynamic capability matching fallback.");
            plannedSteps = PlanWithCapabilityMatching(eventMessage, activeAgents);
        }

        return new TaskPlan(
            PlanId: $"plan_{Guid.NewGuid():N}",
            EventId: eventMessage.EventId,
            CreatedAt: DateTimeOffset.UtcNow,
            Steps: plannedSteps,
            Status: "Created"
        );
    }

    private async Task<List<TaskStep>> PlanWithLlmProviderAsync(
        AgentEventMessage eventMessage,
        string agentContext,
        List<AgentRegistration> activeAgents,
        ILlmProvider llmProvider,
        CancellationToken cancellationToken)
    {
        var systemPrompt = $@"You are a Central Orchestrator Agent. Your job is to analyze unread emails and create an execution plan using our available sub-agents.

Available Sub-Agents Registry:
{agentContext}

Instructions:
1. FIRST, evaluate if the email requires an automated response or action. If the email is a promotional ad, newsletter, receipt, marketing, or no-reply notification that does NOT require an action or reply, return an EMPTY JSON array `[]`.
2. If the email requests data, reports, or database information (e.g., patient lab reports, medical records, sales queries):
   - You MUST generate a 2-step DAG execution plan:
     * STEP 1: Call the database agent (e.g. 'mysql-data-agent' with action 'query_labreports_db' or 'sql-data-agent' with action 'query_database') to fetch the data.
     * STEP 2: Call the email agent (e.g. 'outlook-email-agent' with action 'send_reply' or 'send_email') with `dependsOn: [1]` to email the extracted report back to the user!
3. Output ONLY a valid JSON array of step objects:
[
  {{
    ""stepId"": 1,
    ""agentId"": ""target-agent-id"",
    ""action"": ""capability_action_name"",
    ""parameters"": {{ ""key"": ""value"" }},
    ""dependsOn"": []
  }}
]";

        var userPrompt = $"Event Source: {eventMessage.Source}\nPrompt: {eventMessage.Prompt}\nPayload Data: {eventMessage.Data.GetRawText()}";

        var response = await llmProvider.CompleteAsync(new LlmCompletionRequest(
            Prompt: userPrompt,
            SystemInstruction: systemPrompt,
            Temperature: 0.1f,
            MaxTokens: 1000
        ), cancellationToken);

        var jsonText = response.ResponseText ?? string.Empty;
        _logger.LogInformation("LLM Raw Response: {ResponseText}", jsonText);

        if (jsonText.Contains("```json"))
        {
            jsonText = jsonText.Split("```json")[1].Split("```")[0].Trim();
        }
        else if (jsonText.Contains("```"))
        {
            jsonText = jsonText.Split("```")[1].Split("```")[0].Trim();
        }

        var steps = JsonSerializer.Deserialize<List<TaskStep>>(jsonText, JsonOptions);

        if (steps != null && steps.Count > 0)
        {
            var orderedSteps = steps
                .OrderByDescending(s => s.Action.Contains("query") || s.Action.Contains("analyze") || s.AgentId.Contains("data"))
                .ToList();

            var finalSteps = new List<TaskStep>();
            for (int i = 0; i < orderedSteps.Count; i++)
            {
                int currentStepId = i + 1;
                var dependsOnList = i > 0 ? new List<int> { i } : new List<int>();
                finalSteps.Add(orderedSteps[i] with { StepId = currentStepId, DependsOn = dependsOnList });
            }
            return finalSteps;
        }

        return steps ?? new List<TaskStep>();
    }

    private async Task<List<TaskStep>> PlanWithLlmAsync(
        AgentEventMessage eventMessage,
        string agentContext,
        List<AgentRegistration> activeAgents,
        CancellationToken cancellationToken)
    {
        var systemPrompt = $@"You are a Central Orchestrator Agent. Your job is to analyze unread emails and determine if they require an automated action or response from our personal multi-agent platform.

Available Sub-Agents Registry:
{agentContext}

Instructions:
1. FIRST, evaluate if the email requires an automated response or action. If the email is a promotional ad, newsletter, receipt, marketing, or no-reply notification that does NOT require an action or reply, return an EMPTY JSON array `[]`.
2. If an automated action or response IS required, select which sub-agents to invoke to fulfill the request.
3. Output ONLY a valid JSON array of step objects:
[
  {{
    ""stepId"": 1,
    ""agentId"": ""target-agent-id"",
    ""action"": ""capability_action_name"",
    ""parameters"": {{ ""key"": ""value"" }},
    ""dependsOn"": []
  }}
]";

        var userMessage = $"Event Source: {eventMessage.Source}\nPrompt: {eventMessage.Prompt}\nPayload Data: {eventMessage.Data.GetRawText()}";

        var response = await _chatClient!.GetResponseAsync(
            new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
            },
            cancellationToken: cancellationToken
        );

        try
        {
            var jsonText = response.Choices.FirstOrDefault()?.Text ?? response.ToString();
            if (jsonText.Contains("```json"))
            {
                jsonText = jsonText.Split("```json")[1].Split("```")[0].Trim();
            }
            else if (jsonText.Contains("```"))
            {
                jsonText = jsonText.Split("```")[1].Split("```")[0].Trim();
            }

            var steps = JsonSerializer.Deserialize<List<TaskStep>>(jsonText, JsonOptions);
            return steps ?? new List<TaskStep>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM planner JSON response. Falling back to capability evaluation.");
            return PlanWithCapabilityMatching(eventMessage, activeAgents);
        }
    }

    private List<TaskStep> PlanWithCapabilityMatching(AgentEventMessage eventMessage, List<AgentRegistration> activeAgents)
    {
        var steps = new List<TaskStep>();
        var promptLower = eventMessage.Prompt.ToLower();

        int stepIdCounter = 1;
        int lastStepId = 0;

        foreach (var agent in activeAgents)
        {
            foreach (var cap in agent.Capabilities)
            {
                var capNameLower = cap.CapabilityName.ToLower();
                var capDescLower = cap.Description.ToLower();
                var agentDescLower = agent.Description.ToLower();

                // Common stopwords to exclude from capability keyword matching
                var stopwords = new HashSet<string> { "from", "with", "that", "this", "have", "your", "their", "about", "into", "some", "please", "provide", "response" };

                // Match request intent dynamically against registered agent descriptions and capability actions
                bool matches = promptLower.Split(' ', ',', '.', ':', '\n', '\r', '-').Any(word => 
                {
                    var clean = word.TrimEnd('s');
                    return clean.Length > 3 && !stopwords.Contains(clean) && (capNameLower.Contains(clean) || capDescLower.Contains(clean) || agentDescLower.Contains(clean));
                });

                if (matches)
                {
                    var parameters = new Dictionary<string, object>();
                    if (eventMessage.Data.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in eventMessage.Data.EnumerateObject())
                        {
                            parameters[prop.Name] = prop.Value.ToString();
                        }
                    }
                    parameters["prompt"] = eventMessage.Prompt;

                    if (promptLower.Contains("female") || promptLower.Contains("woman") || promptLower.Contains("women"))
                    {
                        parameters["genderFilter"] = "Female";
                    }
                    else if (promptLower.Contains("male") || promptLower.Contains("man") || promptLower.Contains("men"))
                    {
                        parameters["genderFilter"] = "Male";
                    }

                    var dependsOnList = lastStepId > 0 ? new List<int> { lastStepId } : new List<int>();
                    int currentStepId = stepIdCounter++;

                    steps.Add(new TaskStep(
                        StepId: currentStepId,
                        AgentId: agent.Id,
                        Action: cap.CapabilityName,
                        Parameters: parameters,
                        DependsOn: dependsOnList,
                        Status: "Pending",
                        ResultOutput: null
                    ));
                    break;
                }
            }
        }

        // Order steps logically so data retrieval capabilities (e.g. query_labreports_db) run first, and communication capabilities (e.g. send_email) run second
        var orderedSteps = steps
            .OrderByDescending(s => s.Action.Contains("query") || s.Action.Contains("analyze") || s.AgentId.Contains("data"))
            .ToList();

        var finalSteps = new List<TaskStep>();
        for (int i = 0; i < orderedSteps.Count; i++)
        {
            int currentStepId = i + 1;
            var dependsOnList = i > 0 ? new List<int> { i } : new List<int>();
            finalSteps.Add(orderedSteps[i] with { StepId = currentStepId, DependsOn = dependsOnList });
        }

        return finalSteps;
    }

    public async Task<TaskPlan> ExecutePlanAsync(TaskPlan plan, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing plan {PlanId} with {Count} generic steps", plan.PlanId, plan.Steps.Count);

        var updatedSteps = new List<TaskStep>();

        foreach (var step in plan.Steps)
        {
            _logger.LogInformation("Executing Step {StepId}: {Action} on Agent {AgentId}", step.StepId, step.Action, step.AgentId);

            var agent = await _agentRegistry.GetAgentByIdAsync(step.AgentId);
            if (agent == null || !agent.IsActive)
            {
                _logger.LogWarning("Agent {AgentId} is not active or found in registry database", step.AgentId);
                updatedSteps.Add(step with { Status = "Skipped", ResultOutput = "Agent inactive or missing" });
                continue;
            }

            try
            {
                string resultText;
                if (agent.TransportType == "StreamableHttpMcp")
                {
                    var mcpRes = await _mcpClient.CallToolAsync(agent.EndpointUrl, step.Action, step.Parameters, cancellationToken);
                    resultText = mcpRes.Result?.Content?.FirstOrDefault()?.Text ?? "Tool completed with no text output";
                }
                else
                {
                    // Generic HTTP POST dispatch
                    var content = new StringContent(JsonSerializer.Serialize(step.Parameters, JsonOptions), Encoding.UTF8, "application/json");
                    var httpRes = await _httpClient.PostAsync(agent.EndpointUrl, content, cancellationToken);
                    resultText = await httpRes.Content.ReadAsStringAsync(cancellationToken);
                }

                updatedSteps.Add(step with { Status = "Completed", ResultOutput = resultText });
                _logger.LogInformation("Step {StepId} finished successfully", step.StepId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {StepId} failed execution", step.StepId);
                updatedSteps.Add(step with { Status = "Failed", ResultOutput = ex.Message });
            }
        }

        return plan with
        {
            Steps = updatedSteps,
            Status = updatedSteps.All(s => s.Status == "Completed" || s.Status == "Skipped") ? "Completed" : "CompletedWithErrors"
        };
    }
}
