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
                        var resultMarkdown = await ExecuteRealMySqlPatientQueryAsync(task.PayloadJson, stoppingToken);

                        var resultObj = new
                        {
                            agentId = AgentId,
                            action = task.Action,
                            output = resultMarkdown
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

    private async Task<string> ExecuteRealMySqlPatientQueryAsync(string payloadJson, CancellationToken cancellationToken)
    {
        int limit = 5;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("patientCount", out var countProp) && countProp.TryGetInt32(out int c))
            {
                limit = c;
            }
        }
        catch { }

        _logger.LogInformation("Executing JOIN query across labreports.lab_report and patients_info.vw_hospitalpatientcurrent for last {Count} patients...", limit);

        await using var conn = new MySqlConnection(MySqlConnectionString);
        await conn.OpenAsync(cancellationToken);

        string sql = @"
            SELECT 
                lr.PatientPID,
                p.FirstName,
                p.LastName,
                p.AgeYears,
                p.HospitalName,
                lr.ReportedAt,
                lr.Content,
                ai.Diagnostic,
                ai.PatientAdvice
            FROM labreports.lab_report lr
            LEFT JOIN (
                SELECT DISTINCT PatientId, FirstName, LastName, AgeYears, HospitalName 
                FROM patients_info.vw_hospitalpatientcurrent
            ) p ON lr.PatientPID = p.PatientId
            LEFT JOIN labreports.lab_report_ai ai ON lr.ReportId = ai.ReportId
            ORDER BY lr.CreatedAt DESC
            LIMIT " + limit + ";";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("### 🏥 MySQL Lab Reports & Patients Information Report");
        sb.AppendLine("**Databases Joined:** `labreports.lab_report` ⟗ `patients_info.vw_hospitalpatientcurrent`");
        sb.AppendLine($"**Query Time:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("| # | Patient Name | Age | Hospital | Reported Date | Key Lab Tests & Abnormal Values | AI Diagnostic Summary |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |");

        int rowIdx = 1;
        while (await reader.ReadAsync(cancellationToken))
        {
            string patientPid = reader.IsDBNull(0) ? "N/A" : reader.GetString(0);
            string firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
            string lastName = reader.IsDBNull(2) ? "" : reader.GetString(2);
            string ageStr = reader.IsDBNull(3) ? "N/A" : reader.GetValue(3).ToString()!;
            string hospitalStr = reader.IsDBNull(4) ? "Hospital" : reader.GetString(4);
            string reportedAt = reader.IsDBNull(5) ? "N/A" : reader.GetDateTime(5).ToString("yyyy-MM-dd");
            string contentJson = reader.IsDBNull(6) ? "{}" : reader.GetString(6);
            string aiDiagnostic = reader.IsDBNull(7) ? "Pending AI analysis" : reader.GetString(7);

            string fullName = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = $"Patient GUID: {patientPid[..8]}...";
            }

            var testList = new List<string>();
            try
            {
                using var jsonDoc = JsonDocument.Parse(contentJson);
                if (jsonDoc.RootElement.TryGetProperty("Reports", out var reportsProp) && reportsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var category in reportsProp.EnumerateObject())
                    {
                        if (category.Value.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var test in category.Value.EnumerateObject())
                            {
                                if (test.Value.ValueKind == JsonValueKind.Object)
                                {
                                    string reportVal = test.Value.TryGetProperty("Report", out var rVal) ? rVal.GetString() ?? "" : "";
                                    bool isAbnormal = test.Value.TryGetProperty("IsAbnormal", out var abVal) && abVal.GetBoolean();

                                    if (!string.IsNullOrWhiteSpace(reportVal))
                                    {
                                        string mark = isAbnormal ? " ⚠️ (ABNORMAL)" : "";
                                        testList.Add($"**{test.Name}:** {reportVal.Trim()}{mark}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            string testSummary = testList.Count > 0 ? string.Join("<br/>", testList.Take(4)) : "Laboratory findings recorded";
            string truncatedDiag = aiDiagnostic.Length > 120 ? aiDiagnostic[..120] + "..." : aiDiagnostic;

            sb.AppendLine($"| {rowIdx++} | **{fullName}** | {ageStr} | {hospitalStr} | {reportedAt} | {testSummary} | {truncatedDiag} |");
        }

        sb.AppendLine();
        sb.AppendLine($"*Extracted live from MySQL `labreports` and `patients_info` databases at {DateTimeOffset.UtcNow:u}*");
        return sb.ToString();
    }
}
