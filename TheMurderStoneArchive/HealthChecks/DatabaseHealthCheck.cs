using Microsoft.Extensions.Diagnostics.HealthChecks;
using TheMurderStoneArchive.Data;

namespace TheMurderStoneArchive.HealthChecks
{
    /// <summary>
    /// Health check for database connectivity and basic query execution.
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(ApplicationDbContext context, ILogger<DatabaseHealthCheck> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to execute a simple query to verify database connectivity
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

                if (canConnect)
                {
                    _logger.LogInformation("Database health check passed");
                    return HealthCheckResult.Healthy("Database connection successful");
                }

                _logger.LogError("Database health check failed: Unable to connect");
                return HealthCheckResult.Unhealthy("Unable to connect to database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed with exception");
                return HealthCheckResult.Unhealthy("Database health check failed", ex);
            }
        }
    }
}
