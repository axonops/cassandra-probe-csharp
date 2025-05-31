using CassandraProbe.Actions;
using CassandraProbe.Actions.PortSpecificProbes;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CassandraProbe.Cli.DependencyInjection;

public static class ServiceConfiguration
{
    public static void AddCassandraProbeServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IConnectionMonitor, ConnectionMonitor>();
        
        // Discovery and orchestration
        services.AddScoped<IClusterDiscovery, ClusterDiscoveryService>();
        services.AddScoped<IProbeOrchestrator, ProbeOrchestrator>();
        
        // Probe actions
        services.AddScoped<IProbeAction, SocketProbe>();
        services.AddScoped<IProbeAction, PingProbe>();
        services.AddScoped<IProbeAction, CqlQueryProbe>();
        services.AddScoped<IProbeAction, NativePortProbe>();
        services.AddScoped<IProbeAction, StoragePortProbe>();
    }
}