using CassandraProbe.Actions;
using CassandraProbe.Actions.PortSpecificProbes;
using CassandraProbe.Cli.DependencyInjection;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using CassandraProbe.Logging;
using CassandraProbe.Logging.Formatters;
using CassandraProbe.Scheduling;
using CassandraProbe.Services;
using CassandraProbe.Services.Resilience;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;
using Serilog.Extensions.Logging;

namespace CassandraProbe.Cli;

class Program
{
    private static IServiceProvider? _serviceProvider;
    private static Serilog.ILogger? _serilogLogger;

    static async Task<int> Main(string[] args)
    {
        try
        {
            return await Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult(
                    async options => await RunProbeAsync(options),
                    errors => Task.FromResult(1)
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            // Cleanup
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            if (_serilogLogger is IDisposable disposableLogger)
            {
                disposableLogger.Dispose();
            }
        }
    }

    private static async Task<int> RunProbeAsync(CommandLineOptions options)
    {
        // Handle version request
        if (options.Version)
        {
            Console.WriteLine("Cassandra Probe v1.0.0");
            return 0;
        }

        // Convert options to configuration
        var config = BuildConfiguration(options);

        // Setup logging
        _serilogLogger = ProbeLogger.CreateLogger(config.Logging);
        Log.Logger = _serilogLogger;

        // Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services, config, options);
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Cassandra Probe starting...");

        // Handle resilient client demonstration
        if (options.UseResilientClient)
        {
            logger.LogInformation("Running resilient client demonstration...");
            var resilienceDemo = _serviceProvider.GetRequiredService<ResilienceDemo>();
            ResilienceScenarios.LogScenarios(logger);
            await resilienceDemo.StartDemoAsync();
            return 0;
        }

        // Initialize monitoring services
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>() as SessionManager;
        var metadataMonitor = _serviceProvider.GetRequiredService<MetadataMonitor>();
        var hostStateMonitor = _serviceProvider.GetRequiredService<HostStateMonitor>();
        
        if (sessionManager != null)
        {
            sessionManager.SetMetadataMonitor(metadataMonitor);
        }
        hostStateMonitor.SetMetadataMonitor(metadataMonitor);

        // Start background monitoring services
        var cts = new CancellationTokenSource();
        var metadataTask = metadataMonitor.StartAsync(cts.Token);
        var hostStateTask = hostStateMonitor.StartAsync(cts.Token);

        try
        {
            // Handle scheduled execution
            if (config.Scheduling.IntervalSeconds.HasValue || !string.IsNullOrEmpty(config.Scheduling.CronExpression))
            {
                return await RunScheduledProbesAsync(config, logger);
            }
            else
            {
                // Single run
                return await RunSingleProbeAsync(config, logger, options);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Probe execution failed");
            return 1;
        }
        finally
        {
            // Stop monitoring services
            cts.Cancel();
            try
            {
                await Task.WhenAll(metadataTask, hostStateTask);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
    }

    private static async Task<int> RunSingleProbeAsync(ProbeConfiguration config, ILogger<Program> logger, CommandLineOptions options)
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider is not initialized");
        }
        
        var orchestrator = _serviceProvider.GetRequiredService<IProbeOrchestrator>();
        var connectionMonitor = _serviceProvider.GetRequiredService<IConnectionMonitor>();

        var session = await orchestrator.ExecuteProbesAsync(config);

        // Output results
        await OutputResultsAsync(session, options, logger);

        // Log final connection status
        var poolStatus = connectionMonitor.GetPoolStatus();
        logger.LogInformation(
            "Final connection pool status - Total: {Total}, Active: {Active}, Failed: {Failed}",
            poolStatus.TotalConnections,
            poolStatus.ActiveConnections,
            poolStatus.FailedHosts);

        return session.Results.All(r => r.Success) ? 0 : 10; // Exit code 10 for partial failures
    }

    private static async Task<int> RunScheduledProbesAsync(ProbeConfiguration config, ILogger<Program> logger)
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider is not initialized");
        }
        
        var scheduler = _serviceProvider.GetRequiredService<JobScheduler>();
        var schedulerInstance = await scheduler.StartAsync(config);

        logger.LogInformation("Probe scheduled. Press Ctrl+C to stop...");

        // Wait for cancellation
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Stopping scheduled probes...");
        }

        await scheduler.StopAsync();
        return 0;
    }

    private static async Task OutputResultsAsync(ProbeSession session, CommandLineOptions options, ILogger<Program> logger)
    {
        string output = options.OutputFormat.ToLower() switch
        {
            "json" => JsonFormatter.FormatSession(session),
            "csv" => CsvFormatter.FormatSession(session),
            _ => FormatConsoleOutput(session)
        };

        if (!string.IsNullOrEmpty(options.OutputFile))
        {
            await File.WriteAllTextAsync(options.OutputFile, output);
            logger.LogInformation("Results written to {OutputFile}", options.OutputFile);
        }
        else if (!options.Quiet)
        {
            Console.WriteLine(output);
        }
    }

    private static string FormatConsoleOutput(ProbeSession session)
    {
        var lines = new List<string>
        {
            "",
            "=== Probe Session Results ===",
            $"Session ID: {session.Id}",
            $"Duration: {session.Duration:g}",
            ""
        };

        if (session.Topology != null)
        {
            lines.Add($"Cluster: {session.Topology.ClusterName}");
            lines.Add($"Total Hosts: {session.Topology.TotalHosts} (Up: {session.Topology.UpHosts}, Down: {session.Topology.DownHosts})");
            lines.Add("");
        }

        lines.Add("Probe Results:");
        lines.Add("--------------");

        foreach (var result in session.Results.OrderBy(r => r.Host.Address.ToString()).ThenBy(r => r.ProbeType))
        {
            var status = result.Success ? "✓" : "✗";
            var duration = $"{result.Duration.TotalMilliseconds:F2}ms";
            var error = result.Success ? "" : $" - {result.ErrorMessage}";
            
            lines.Add($"{status} {result.Host.Address}:{result.Host.NativePort} [{result.ProbeType}] {duration}{error}");
        }

        lines.Add("");
        lines.Add($"Summary: {session.Results.Count(r => r.Success)}/{session.Results.Count} successful");

        return string.Join(Environment.NewLine, lines);
    }

    private static ProbeConfiguration BuildConfiguration(CommandLineOptions options)
    {
        var config = new ProbeConfiguration
        {
            ContactPoints = options.ContactPoints.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            
            Authentication = new AuthenticationSettings
            {
                Username = options.Username ?? string.Empty,
                Password = options.Password ?? string.Empty,
                CqlshrcPath = options.CqlshrcPath ?? string.Empty
            },
            
            Connection = new ConnectionSettings
            {
                Port = options.Port,
                UseSsl = options.UseSsl,
                CertificatePath = options.CertificatePath ?? string.Empty,
                CaCertificatePath = options.CaCertificatePath ?? string.Empty
            },
            
            ProbeSelection = new ProbeSelectionSettings
            {
                ProbeNativePort = options.ProbeNative,
                ProbeStoragePort = options.ProbeStorage,
                ProbePing = options.ProbePing,
                ExecuteAllProbes = options.AllProbes,
                SocketTimeoutMs = options.SocketTimeout
            },
            
            Query = new QuerySettings
            {
                TestCql = options.TestCql ?? string.Empty,
                ConsistencyLevel = options.ConsistencyLevel,
                EnableTracing = options.EnableTracing,
                QueryTimeoutSeconds = options.QueryTimeout
            },
            
            Logging = new LoggingSettings
            {
                LogDirectory = options.LogDirectory,
                MaxDaysToKeep = options.LogMaxDays,
                MaxFileSizeMb = options.LogMaxFileMb,
                LogFormat = options.LogFormat,
                Quiet = options.Quiet,
                Verbose = options.Verbose,
                LogLevel = options.LogLevel,
                LogReconnections = options.LogReconnections,
                ShowConnectionEvents = options.ShowConnectionEvents
            },
            
            Scheduling = new SchedulingSettings
            {
                IntervalSeconds = options.IntervalSeconds,
                CronExpression = options.CronExpression ?? string.Empty,
                DurationMinutes = options.DurationMinutes,
                MaxRuns = options.MaxRuns
            }
        };

        // Handle environment variables
        config.ContactPoints = GetFromEnvironment("CASSANDRA_CONTACT_POINTS", config.ContactPoints);
        config.Authentication.Username = Environment.GetEnvironmentVariable("CASSANDRA_USERNAME") ?? config.Authentication.Username;
        config.Authentication.Password = Environment.GetEnvironmentVariable("CASSANDRA_PASSWORD") ?? config.Authentication.Password;
        config.Logging.LogDirectory = Environment.GetEnvironmentVariable("CASSANDRA_PROBE_LOG_DIR") ?? config.Logging.LogDirectory;

        return config;
    }

    private static List<string> GetFromEnvironment(string key, List<string> defaultValue)
    {
        var envValue = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(envValue) 
            ? defaultValue 
            : envValue.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void ConfigureServices(IServiceCollection services, ProbeConfiguration config, CommandLineOptions options)
    {
        // Configuration
        services.AddSingleton(config);

        // Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        // Core services
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IConnectionMonitor, ConnectionMonitor>();
        services.AddScoped<IClusterDiscovery, ClusterDiscoveryService>();
        services.AddScoped<IProbeOrchestrator, ProbeOrchestrator>();
        
        // Monitoring services
        services.AddSingleton<MetadataMonitor>();
        services.AddSingleton<HostStateMonitor>();

        // Resilient client services (if enabled)
        if (options.UseResilientClient)
        {
            services.AddSingleton<IResilientCassandraClient>(provider => 
                new ResilientCassandraClient(
                    provider.GetRequiredService<ProbeConfiguration>(),
                    provider.GetRequiredService<ILogger<ResilientCassandraClient>>()));
            services.AddSingleton<ResilienceDemo>();
        }

        // Probe actions
        services.AddScoped<IProbeAction, SocketProbe>();
        services.AddScoped<IProbeAction, PingProbe>();
        services.AddScoped<IProbeAction, CqlQueryProbe>();
        services.AddScoped<IProbeAction, NativePortProbe>();
        services.AddScoped<IProbeAction, StoragePortProbe>();

        // Scheduling
        services.AddQuartz();
        services.AddScoped<ProbeJob>();
        services.AddSingleton<JobScheduler>();
    }
}