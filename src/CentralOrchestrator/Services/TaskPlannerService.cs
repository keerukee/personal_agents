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
        var systemPrompt = $@"You are a Central Orchestrator Agent. Your task is to analyze inbound user requests and dynamically match them to registered sub-agents based on their descriptions and capabilities.

Available Sub-Agents Registry:
{agentContext}

Instructions:
1. Select which sub-agents to invoke to fulfill the request.
2. Output ONLY a valid JSON array of step objects with format:
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
            // Clean markdown code fence if LLM returns ```json
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
            _logger.LogWarning(ex, "Failed to parse LLM planner JSON response. Falling back to dynamic capability matching.");
            return PlanWithCapabilityMatching(eventMessage, activeAgents);
        }
    }

    private List<TaskStep> PlanWithCapabilityMatching(AgentEventMessage eventMessage, List<AgentRegistration> activeAgents)
    {
        var steps = new List<TaskStep>();
        int stepIdCounter = 1;
        var promptLower = eventMessage.Prompt.ToLower();

        foreach (var agent in activeAgents)
        {
            foreach (var cap in agent.Capabilities)
            {
                var capNameLower = cap.CapabilityName.ToLower();
                var capDescLower = cap.Description.ToLower();

                // Match request intent against agent capability descriptions dynamically
                bool matches = promptLower.Split(' ').Any(word => word.Length > 3 && (capNameLower.Contains(word) || capDescLower.Contains(word)));

                if (matches)
                {
                    var parameters = new Dictionary<string, object>();
                    
                    // Extract payload parameters generically
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
