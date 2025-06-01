using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Cassandra;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Exceptions;
using CassandraProbe.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Services;

public class SessionManager : ISessionManager, IDisposable
{
    private static readonly object _lock = new();
    private static ICluster? _cluster;
    private static ISession? _session;
    private readonly ILogger<SessionManager> _logger;
    private readonly IConnectionMonitor _connectionMonitor;
    private readonly ProbeConfiguration _configuration;
    private bool _disposed;

    public SessionManager(
        ILogger<SessionManager> logger,
        IConnectionMonitor connectionMonitor,
        ProbeConfiguration configuration)
    {
        _logger = logger;
        _connectionMonitor = connectionMonitor;
        _configuration = configuration;
    }

    public async Task<ISession> GetSessionAsync()
    {
        if (_session == null)
        {
            lock (_lock)
            {
                if (_session == null)
                {
                    _logger.LogInformation("Creating new Cluster instance (one-time operation)");
                    _cluster = CreateCluster();
                    RegisterEventHandlers();
                    _connectionMonitor.RegisterCluster(_cluster);
                    
                    try
                    {
                        _session = _cluster.Connect();
                        _logger.LogInformation("Session established and will be reused for all operations");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to establish initial connection to cluster");
                        throw new ConnectionException("Failed to connect to Cassandra cluster", ex);
                    }
                }
            }
        }
        
        return await Task.FromResult(_session);
    }

    public ICluster? GetCluster() => _cluster;

    private ICluster CreateCluster()
    {
        if (_configuration.ContactPoints.Count == 0)
            throw new ConfigurationException("No contact points specified");

        var builder = Cluster.Builder();

        // Add contact points
        foreach (var contactPoint in _configuration.ContactPoints)
        {
            var parts = contactPoint.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                builder.AddContactPoint(parts[0]).WithPort(port);
            }
            else
            {
                builder.AddContactPoint(contactPoint);
            }
        }

        // Set default port if specified
        if (_configuration.Connection.Port != 9042)
        {
            builder.WithPort(_configuration.Connection.Port);
        }

        // Authentication
        if (!string.IsNullOrEmpty(_configuration.Authentication.Username) && 
            !string.IsNullOrEmpty(_configuration.Authentication.Password))
        {
            builder.WithCredentials(_configuration.Authentication.Username, _configuration.Authentication.Password);
            _logger.LogInformation("Using username/password authentication");
        }
        else if (!string.IsNullOrEmpty(_configuration.Authentication.CqlshrcPath))
        {
            var cqlshrc = CqlshrcParser.Parse(_configuration.Authentication.CqlshrcPath);
            if (!string.IsNullOrEmpty(cqlshrc.Username) && !string.IsNullOrEmpty(cqlshrc.Password))
            {
                builder.WithCredentials(cqlshrc.Username, cqlshrc.Password);
                _logger.LogInformation("Using authentication from CQLSHRC file");
            }
        }

        // SSL Configuration
        if (_configuration.Connection.UseSsl)
        {
            var sslOptions = new SSLOptions()
                .SetRemoteCertValidationCallback(ValidateServerCertificate);

            if (!string.IsNullOrEmpty(_configuration.Connection.CertificatePath))
            {
                var cert = X509CertificateLoader.LoadCertificateFromFile(_configuration.Connection.CertificatePath);
                sslOptions.SetCertificateCollection(new X509CertificateCollection { cert });
            }

            builder.WithSSL(sslOptions);
            _logger.LogInformation("SSL/TLS enabled for connections");
        }

        // Connection timeouts
        builder.WithSocketOptions(new SocketOptions()
            .SetConnectTimeoutMillis(_configuration.Connection.ConnectionTimeoutSeconds * 1000)
            .SetReadTimeoutMillis(_configuration.Connection.RequestTimeoutSeconds * 1000));

        // Query options
        builder.WithQueryOptions(new QueryOptions()
            .SetConsistencyLevel(ParseConsistencyLevel(_configuration.Query.ConsistencyLevel)));

        // Load balancing policy - use TokenAwarePolicy wrapping DCAwareRoundRobinPolicy
        // We don't specify a local DC, letting the driver auto-discover it
        builder.WithLoadBalancingPolicy(new TokenAwarePolicy(new DCAwareRoundRobinPolicy()));

        // Reconnection policy - exponential with max 60 seconds
        builder.WithReconnectionPolicy(new ExponentialReconnectionPolicy(1000, 60000));

        return builder.Build();
    }

    private void RegisterEventHandlers()
    {
        if (_cluster == null) return;

        // Register all cluster event handlers
        _cluster.HostAdded += OnHostAdded;
        _cluster.HostRemoved += OnHostRemoved;
        
        _logger.LogInformation("Cluster event handlers registered for topology changes");
    }

    private void OnHostAdded(Host host)
    {
        _logger.LogInformation("[CLUSTER EVENT] Node ADDED: {Address} DC={Datacenter} Rack={Rack}", 
            host.Address, host.Datacenter, host.Rack);
        _connectionMonitor.RecordHostAdded(host);
    }

    private void OnHostRemoved(Host host)
    {
        _logger.LogInformation("[CLUSTER EVENT] Node REMOVED: {Address}", host.Address);
        _connectionMonitor.RecordHostRemoved(host);
    }

    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, 
        X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        _logger.LogWarning("SSL certificate validation failed: {Errors}", sslPolicyErrors);
        
        // In production, you might want to be more strict
        return true;
    }

    private ConsistencyLevel ParseConsistencyLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "ANY" => ConsistencyLevel.Any,
            "ONE" => ConsistencyLevel.One,
            "TWO" => ConsistencyLevel.Two,
            "THREE" => ConsistencyLevel.Three,
            "QUORUM" => ConsistencyLevel.Quorum,
            "ALL" => ConsistencyLevel.All,
            "LOCAL_QUORUM" => ConsistencyLevel.LocalQuorum,
            "EACH_QUORUM" => ConsistencyLevel.EachQuorum,
            "LOCAL_ONE" => ConsistencyLevel.LocalOne,
            _ => ConsistencyLevel.One
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing SessionManager");

        lock (_lock)
        {
            _session?.Dispose();
            _cluster?.Dispose();
            _session = null;
            _cluster = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}