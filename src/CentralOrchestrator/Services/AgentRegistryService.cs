using CentralOrchestrator.Data;
using CentralOrchestrator.Data.Entities;
using Common.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CentralOrchestrator.Services;

public class AgentRegistryService : IAgentRegistryService
{
    private readonly AgentRegistryDbContext _dbContext;
    private readonly ILogger<AgentRegistryService> _logger;

    public AgentRegistryService(AgentRegistryDbContext dbContext, ILogger<AgentRegistryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<AgentRegistration>> GetAllActiveAgentsAsync()
    {
        var entities = await _dbContext.Agents
            .Include(a => a.Capabilities)
            .Where(a => a.IsActive)
            .AsNoTracking()
            .ToListAsync();

        return entities.Select(MapToRegistration).ToList();
    }

    public async Task<AgentRegistration?> GetAgentByIdAsync(string id)
    {
        var entity = await _dbContext.Agents
            .Include(a => a.Capabilities)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        return entity != null ? MapToRegistration(entity) : null;
    }

    public async Task<List<AgentRegistration>> GetAgentsByCapabilityAsync(string capabilityName)
    {
        var entities = await _dbContext.Agents
            .Include(a => a.Capabilities)
            .Where(a => a.IsActive && a.Capabilities.Any(c => c.CapabilityName.ToLower() == capabilityName.ToLower()))
            .AsNoTracking()
            .ToListAsync();

        return entities.Select(MapToRegistration).ToList();
    }

    public async Task<AgentRegistration> RegisterOrUpdateAgentAsync(RegisterAgentRequest request)
    {
        var existing = await _dbContext.Agents
            .Include(a => a.Capabilities)
            .FirstOrDefaultAsync(a => a.Id == request.Id);

        var now = DateTimeOffset.UtcNow;

        if (existing == null)
        {
            existing = new AgentEntity
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                EndpointUrl = request.EndpointUrl,
                TransportType = request.TransportType,
                IsActive = true,
                RegisteredAt = now,
                LastHeartbeat = now,
                Capabilities = request.Capabilities.Select(c => new AgentCapabilityEntity
                {
                    AgentId = request.Id,
                    CapabilityName = c.CapabilityName,
                    Description = c.Description,
                    ParametersJsonSchema = c.ParametersJsonSchema ?? "{}"
                }).ToList()
            };
            _dbContext.Agents.Add(existing);
            _logger.LogInformation("Registering new agent '{AgentId}' ({AgentName}) at {Endpoint}", existing.Id, existing.Name, existing.EndpointUrl);
        }
        else
        {
            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.EndpointUrl = request.EndpointUrl;
            existing.TransportType = request.TransportType;
            existing.IsActive = true;
            existing.LastHeartbeat = now;

            _dbContext.Capabilities.RemoveRange(existing.Capabilities);
            existing.Capabilities = request.Capabilities.Select(c => new AgentCapabilityEntity
            {
                AgentId = existing.Id,
                CapabilityName = c.CapabilityName,
                Description = c.Description,
                ParametersJsonSchema = c.ParametersJsonSchema ?? "{}"
            }).ToList();

            _logger.LogInformation("Updated existing agent '{AgentId}' capabilities", existing.Id);
        }

        await _dbContext.SaveChangesAsync();
        return MapToRegistration(existing);
    }

    public async Task<bool> DeactivateAgentAsync(string id)
    {
        var agent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == id);
        if (agent == null) return false;

        agent.IsActive = false;
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Agent '{AgentId}' deactivated", id);
        return true;
    }

    public async Task<bool> RecordHeartbeatAsync(string id)
    {
        var agent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == id);
        if (agent == null) return false;

        agent.LastHeartbeat = DateTimeOffset.UtcNow;
        agent.IsActive = true;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task SeedDefaultAgentsAsync()
    {
        if (await _dbContext.Agents.AnyAsync())
        {
            return;
        }

        _logger.LogInformation("Seeding initial default agent registry into SQLite DB...");

        var defaults = new List<RegisterAgentRequest>
        {
            new(
                "sql-data-agent",
                "SqlDataAgent",
                "Executes SQL queries against regional database and formats tabular results",
                "http://localhost:5001/mcp",
                "StreamableHttpMcp",
                new List<AgentCapabilityDto>
                {
                    new("query_database", "Run parameterized read-only SQL queries", "{\"type\":\"object\",\"properties\":{\"sql\":{\"type\":\"string\"},\"format\":{\"type\":\"string\"}}}")
                }
            ),
            new(
                "python-runner-agent",
                "PythonRunnerAgent",
                "Dynamically inspects dependencies, installs packages, and executes Python code in sandbox",
                "http://localhost:5002/execute-script",
                "PythonFastApi",
                new List<AgentCapabilityDto>
                {
                    new("execute_script", "Run python code snippet with dynamic dependencies", "{\"type\":\"object\",\"properties\":{\"code\":{\"type\":\"string\"}}}")
                }
            ),
            new(
                "doc-intelligence-gateway",
                "AzureDocIntelligenceGateway",
                "Extracts structured text, tables, and key-value pairs from attachments",
                "http://localhost:5003/api/shared/doc-intelligence",
                "RestApi",
                new List<AgentCapabilityDto>
                {
                    new("analyze_document", "Analyze document attachment from local staging file", "{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"}}}")
                }
            ),
            new(
                "outlook-email-agent",
                "OutlookEmailAgent",
                "Sends emails or replies to email threads via local desktop Outlook COM",
                "http://localhost:5000/api/email/reply",
                "PythonFastApi",
                new List<AgentCapabilityDto>
                {
                    new("send_reply", "Send or reply to an email using local MAPI desktop profile", "{\"type\":\"object\",\"properties\":{\"targetEntryId\":{\"type\":\"string\"},\"htmlBody\":{\"type\":\"string\"}}}")
                }
            )
        };

        foreach (var req in defaults)
        {
            await RegisterOrUpdateAgentAsync(req);
        }

        _logger.LogInformation("Default agent registry seeding complete.");
    }

    private static AgentRegistration MapToRegistration(AgentEntity entity)
    {
        return new AgentRegistration(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.EndpointUrl,
            entity.TransportType,
            entity.IsActive,
            entity.RegisteredAt,
            entity.LastHeartbeat,
            entity.Capabilities.Select(c => new AgentCapabilityDto(c.CapabilityName, c.Description, c.ParametersJsonSchema)).ToList()
        );
    }
}
