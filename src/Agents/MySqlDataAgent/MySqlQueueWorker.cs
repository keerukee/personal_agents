using CentralOrchestrator.Services;
using Common.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Text;
using System.Text.Json;

namespace MySqlDataAgent;

public class MySqlQueueWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MySqlQueueWorker> _logger;
    private const string AgentId = "mysql-data-agent";
    private const string MySqlConnectionString = "Server=localhost;Port=3306;Database=labreports;Uid=root;Pwd=root;";

    public MySqlQueueWorker(IServiceProvider serviceProvider, ILogger<MySqlQueueWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MySqlDataAgent Database Queue Worker started. Agent ID: {AgentId}", AgentId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<IDatabaseQueueService>();

                var pendingTasks = await queueService.GetPendingTasksForAgentAsync(AgentId, limit: 5);

                foreach (var task in pendingTasks)
                {
                    _logger.LogInformation("Claiming Task '{TaskGuid}' (Action: '{Action}')", task.TaskGuid, task.Action);
                    
                    var claimed = await queueService.ClaimTaskAsync(task.TaskGuid);
                    if (!claimed) continue;

                    try
                    {
                        var resultHtml = await ExecuteRealMySqlPatientQueryAsync(task.PayloadJson, scope.ServiceProvider, stoppingToken);

                        var resultObj = new
                        {
                            agentId = AgentId,
                            action = task.Action,
                            output = resultHtml
                        };

                        await queueService.UpdateTaskResultAsync(task.TaskGuid, new UpdateTaskResultRequest(
                            Status: "Completed",
                            ResultJson: JsonSerializer.Serialize(resultObj)
                        ));

                        _logger.LogInformation("Successfully completed Task '{TaskGuid}'", task.TaskGuid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute Task '{TaskGuid}'", task.TaskGuid);
                        await queueService.UpdateTaskResultAsync(task.TaskGuid, new UpdateTaskResultRequest(
                            Status: "Failed",
                            ErrorMessage: ex.Message
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MySqlQueueWorker loop");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private async Task<string> ExecuteRealMySqlPatientQueryAsync(string payloadJson, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // 1. Get LLM provider
        var aiFactory = serviceProvider.GetRequiredService<CentralOrchestrator.Services.AI.IAiProviderFactory>();
        var llm = aiFactory.GetLlmProvider();

        string taskDesc = "Query patient lab reports.";
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("task", out var taskProp))
            {
                taskDesc = taskProp.GetString() ?? taskDesc;
            }
        }
        catch { }

        _logger.LogInformation("Agent-Local LLM processing task: {TaskDesc}", taskDesc);

        // 2. Generate SQL via LLM
        var sqlSchemaPrompt = @"You are an expert MySQL database agent. Your job is to generate a SQL query based on a natural language request.
You have access to the following databases and tables on localhost:3306.

Database: labreports
Table: lab_report (ReportId VARCHAR, PatientPID VARCHAR, ReportedAt DATETIME, Content JSON, CreatedAt DATETIME)
Table: lab_report_ai (ReportId VARCHAR, Diagnostic TEXT, PatientAdvice TEXT)

Database: patients_info
Table: vw_hospitalpatientcurrent (PatientId VARCHAR, FirstName VARCHAR, LastName VARCHAR, AgeYears INT, HospitalName VARCHAR, SexAtBirth VARCHAR)

Relationships: 
- labreports.lab_report.PatientPID = patients_info.vw_hospitalpatientcurrent.PatientId
- labreports.lab_report.ReportId = labreports.lab_report_ai.ReportId

Notes on Data:
- SexAtBirth values: '1' or 'Female' or 'F' means Female. '2' or 'Male' or 'M' means Male.
- The 'Content' field in lab_report contains JSON with laboratory test results.

Instructions:
Generate ONLY the raw MySQL query to fulfill the user's task. 
Do NOT wrap the output in markdown code blocks like ```sql ... ```. Output ONLY the raw SQL string.
LIMIT results to a reasonable amount (e.g. 5) if not specified.";

        var sqlResponse = await llm.CompleteAsync(new Common.Contracts.LlmCompletionRequest(
            Prompt: taskDesc,
            SystemInstruction: sqlSchemaPrompt,
            Temperature: 0.1f,
            MaxTokens: 2000
        ), cancellationToken);

        string sql = sqlResponse.ResponseText?.Trim() ?? "";
        if (sql.StartsWith("```sql"))
        {
            sql = sql.Split("```sql")[1].Split("```")[0].Trim();
        }
        else if (sql.StartsWith("```"))
        {
            sql = sql.Split("```")[1].Split("```")[0].Trim();
        }

        _logger.LogInformation("Agent-Local LLM generated SQL: {Sql}", sql);

        // 3. Execute SQL
        var results = new List<Dictionary<string, object>>();
        await using var conn = new MySqlConnection(MySqlConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i);
                var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                
                if (val is DateTime dt)
                {
                    row[colName] = dt.ToString("o");
                }
                else if (val is string strVal && (strVal.TrimStart().StartsWith("{") || strVal.TrimStart().StartsWith("[")))
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(strVal);
                        CleanRtfNodes(node);
                        row[colName] = node;
                    }
                    catch
                    {
                        row[colName] = strVal;
                    }
                }
                else
                {
                    row[colName] = val;
                }
            }
            results.Add(row!);
        }

        string rawResultsJson = JsonSerializer.Serialize(results);
        _logger.LogInformation("SQL execution returned {Count} rows.", results.Count);

        // 4. Format Results via LLM
        var formatPrompt = @"You are a report formatting agent. Your job is to format raw JSON database query results into a clean, professional HTML report.
The report will be sent via email. 

Instructions:
1. Analyze the JSON results and create a summary of the findings.
2. Format the data into an HTML table with clear headers. Use styling for the table borders and headers.
3. If the data contains JSON content (like the `Content` field with lab report test values), parse them and display the key abnormal tests clearly in the table instead of dumping raw JSON.
4. Output ONLY the raw HTML string. Do NOT wrap in markdown blocks like ```html ... ```.";

        var formatResponse = await llm.CompleteAsync(new Common.Contracts.LlmCompletionRequest(
            Prompt: $"Task: {taskDesc}\n\nRaw JSON Results from DB:\n{rawResultsJson}",
            SystemInstruction: formatPrompt,
            Temperature: 0.2f,
            MaxTokens: 8192
        ), cancellationToken);

        string finalHtml = formatResponse.ResponseText?.Trim() ?? "";
        if (finalHtml.StartsWith("```html"))
        {
            finalHtml = finalHtml.Split("```html")[1].Split("```")[0].Trim();
        }
        else if (finalHtml.StartsWith("```"))
        {
            finalHtml = finalHtml.Split("```")[1].Split("```")[0].Trim();
        }

        _logger.LogInformation("Agent-Local LLM generated HTML report length: {Length}", finalHtml.Length);
        
        return finalHtml;
    }

    private void CleanRtfNodes(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            if (obj.ContainsKey("AbnormalDesc"))
            {
                obj.Remove("AbnormalDesc");
            }
            
            foreach (var kvp in obj.ToArray())
            {
                CleanRtfNodes(kvp.Value);
            }
        }
        else if (node is System.Text.Json.Nodes.JsonArray arr)
        {
            foreach (var item in arr)
            {
                CleanRtfNodes(item);
            }
        }
    }
}
