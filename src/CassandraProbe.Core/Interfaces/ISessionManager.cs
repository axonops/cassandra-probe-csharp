using Cassandra;

namespace CassandraProbe.Core.Interfaces;

public interface ISessionManager
{
    Task<ISession> GetSessionAsync();
    ICluster? GetCluster();
    void Dispose();
}