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
        IChatClient? chatClient = null)
    {
        _agentRegistry = agentRegistry;
        _mcpClient = mcpClient;
        _httpClient = httpClient;
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

        // 3. Use LLM if IChatClient is available, otherwise perform dynamic capability matching
        if (_chatClient != null)
        {
            plannedSteps = await PlanWithLlmAsync(eventMessage, agentContextBuilder.ToString(), activeAgents, cancellationToken);
        }
        else
        {
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

        // 1. Evaluate if this email requires an automated response or action
        bool isAgentRequest = promptLower.Contains("report") ||
                              promptLower.Contains("patient") ||
                              promptLower.Contains("database") ||
                              promptLower.Contains("mysql") ||
                              promptLower.Contains("sql") ||
                              promptLower.Contains("send") ||
                              promptLower.Contains("fetch") ||
                              promptLower.Contains("query") ||
                              promptLower.Contains("task") ||
                              promptLower.Contains("please");

        if (!isAgentRequest)
        {
            _logger.LogInformation("Evaluator: Inbound email does NOT require an automated agent response. Skipping task creation.");
            return steps; // Returns empty list (0 tasks created)
        }

        // 2. Check if request requires MySQL lab report extraction
        if (promptLower.Contains("patient") || promptLower.Contains("lab") || promptLower.Contains("mysql"))
        {
            var mysqlParams = new Dictionary<string, object>();
            if (eventMessage.Data.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in eventMessage.Data.EnumerateObject())
                {
                    mysqlParams[prop.Name] = prop.Value.ToString();
                }
            }
            mysqlParams["prompt"] = eventMessage.Prompt;

            if (promptLower.Contains("female") || promptLower.Contains("woman") || promptLower.Contains("women"))
            {
                mysqlParams["genderFilter"] = "Female";
            }
            else if (promptLower.Contains("male") || promptLower.Contains("man") || promptLower.Contains("men"))
            {
                mysqlParams["genderFilter"] = "Male";
            }

            // Step 1: Query MySQL lab reports database
            steps.Add(new TaskStep(
                StepId: 1,
                AgentId: "mysql-data-agent",
                Action: "query_labreports_db",
                Parameters: mysqlParams,
                DependsOn: new List<int>(),
                Status: "Pending",
                ResultOutput: null
            ));

            // Step 2: Send extracted patient report via Outlook email
            var emailParams = new Dictionary<string, object>(mysqlParams);
            if (eventMessage.Data.TryGetProperty("sender", out var senderProp))
            {
                emailParams["to"] = senderProp.GetString() ?? "keerukee@outlook.com";
            }
            if (eventMessage.Data.TryGetProperty("subject", out var subjProp))
            {
                emailParams["subject"] = $"Re: {subjProp.GetString()}";
            }

            steps.Add(new TaskStep(
                StepId: 2,
                AgentId: "outlook-email-agent",
                Action: "send_email",
                Parameters: emailParams,
                DependsOn: new List<int> { 1 },
                Status: "Pending",
                ResultOutput: null
            ));

            return steps;
        }

        int stepIdCounter = 1;
        foreach (var agent in activeAgents)
        {
            foreach (var cap in agent.Capabilities)
            {
                var capNameLower = cap.CapabilityName.ToLower();
                var capDescLower = cap.Description.ToLower();

                bool matches = promptLower.Split(' ').Any(word => word.Length > 4 && (capNameLower.Contains(word) || capDescLower.Contains(word)));

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

                    steps.Add(new TaskStep(
                        StepId: stepIdCounter++,
                        AgentId: agent.Id,
                        Action: cap.CapabilityName,
                        Parameters: parameters,
                        DependsOn: steps.Select(s => s.StepId).ToList(),
                        Status: "Pending",
                        ResultOutput: null
                    ));
                    break;
                }
            }
        }

        return steps;
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
