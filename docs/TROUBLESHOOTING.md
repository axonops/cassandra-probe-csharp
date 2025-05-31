# Troubleshooting Guide

This guide helps you resolve common issues when using Cassandra Probe.

## Connection Issues

### Cannot Connect to Any Contact Point

**Symptoms:**
```
ERROR - Failed to connect to any contact point
ERROR - No hosts available for the control connection
```

**Causes and Solutions:**

1. **Cassandra is not running**
   ```bash
   # Check if Cassandra is running
   systemctl status cassandra
   # or
   ps aux | grep cassandra
   
   # Start Cassandra if needed
   sudo systemctl start cassandra
   ```

2. **Wrong port or hostname**
   ```bash
   # Test connectivity
   telnet localhost 9042
   # or
   nc -zv localhost 9042
   
   # Check Cassandra is listening
   netstat -tlnp | grep 9042
   ```

3. **Firewall blocking connection**
   ```bash
   # Check firewall rules
   sudo iptables -L -n | grep 9042
   
   # Allow port 9042
   sudo ufw allow 9042/tcp
   ```

4. **Cassandra bound to different interface**
   ```yaml
   # Check cassandra.yaml
   # rpc_address should not be 0.0.0.0 for remote connections
   rpc_address: 192.168.1.10
   # or
   rpc_interface: eth0
   ```

### Authentication Failures

**Symptoms:**
```
ERROR - Authentication failed: Provided username and/or password are incorrect
```

**Solutions:**

1. **Verify credentials**
   ```bash
   # Test with cqlsh
   cqlsh -u cassandra -p cassandra localhost
   ```

2. **Check if authentication is enabled**
   ```yaml
   # In cassandra.yaml
   authenticator: PasswordAuthenticator
   # Default is AllowAllAuthenticator
   ```

3. **Reset password if needed**
   ```sql
   -- Connect with default superuser
   cqlsh -u cassandra -p cassandra
   
   -- Change password
   ALTER USER cassandra WITH PASSWORD 'newpassword';
   ```

### Connection Timeout

**Symptoms:**
```
ERROR - Connection timeout after 10000ms
ERROR - The host 192.168.1.10:9042 did not reply before timeout
```

**Solutions:**

1. **Increase timeout values**
   ```bash
   ./cassandra-probe --contact-points slow-cluster:9042 \
     --connection-timeout 30000 \
     --request-timeout 30000
   ```

2. **Check cluster load**
   ```bash
   nodetool tpstats
   nodetool status
   ```

3. **Network latency issues**
   ```bash
   # Test network latency
   ping -c 10 cassandra-node
   traceroute cassandra-node
   ```

## SSL/TLS Issues

### SSL Handshake Failure

**Symptoms:**
```
ERROR - SSL handshake failed
ERROR - The remote certificate is invalid
```

**Solutions:**

1. **Verify certificate**
   ```bash
   # Check certificate validity
   openssl x509 -in client-cert.pem -text -noout
   
   # Test SSL connection
   openssl s_client -connect cassandra-node:9042 -cert client-cert.pem
   ```

2. **Correct certificate path**
   ```bash
   ./cassandra-probe --contact-points secure-cluster:9042 \
     --ssl \
     --ssl-cert /absolute/path/to/cert.pem
   ```

3. **Disable validation for testing**
   ```bash
   ./cassandra-probe --contact-points secure-cluster:9042 \
     --ssl \
     --ssl-no-verify
   ```

## Version Compatibility

### Unsupported Cassandra Version

**Symptoms:**
```
ERROR - Cassandra version 3.11.x is not supported. Minimum version is 4.0
```

**Solutions:**

1. **Check Cassandra version**
   ```bash
   nodetool version
   # or
   cqlsh -e "SELECT release_version FROM system.local"
   ```

2. **Upgrade Cassandra** to 4.0 or later

3. **Use original Java probe** for Cassandra 3.x:
   ```bash
   # For Cassandra 3.x
   java -jar cassandra-probe-java.jar
   ```

## Performance Issues

### Slow Probe Execution

**Symptoms:**
- Probes taking longer than expected
- Timeouts on healthy clusters

**Solutions:**

1. **Check cluster performance**
   ```bash
   # Check thread pools
   nodetool tpstats
   
   # Check pending compactions
   nodetool compactionstats
   
   # Check GC pressure
   nodetool gcstats
   ```

2. **Reduce probe frequency**
   ```bash
   # Increase interval between probes
   ./cassandra-probe --contact-points cluster:9042 -i 60
   ```

3. **Use specific probes only**
   ```bash
   # Skip expensive CQL probes
   ./cassandra-probe --contact-points cluster:9042 \
     --socket-probe \
     --native-probe
   ```

## Output Issues

### No Output or Missing Data

**Symptoms:**
- No output displayed
- JSON/CSV files empty

**Solutions:**

1. **Check output settings**
   ```bash
   # Ensure output format is specified
   ./cassandra-probe --contact-points cluster:9042 \
     --output json \
     --output-file probe.json
   ```

2. **Verify file permissions**
   ```bash
   # Check write permissions
   touch /var/log/probe.json
   ls -la /var/log/
   ```

3. **Check log level**
   ```bash
   # Enable debug logging
   ./cassandra-probe --contact-points cluster:9042 \
     --log-level DEBUG
   ```

## Platform-Specific Issues

### macOS Security Warning

**Symptoms:**
```
"cassandra-probe" cannot be opened because the developer cannot be verified
```

**Solution:**
```bash
# Remove quarantine attribute
xattr -d com.apple.quarantine cassandra-probe

# Or allow in System Preferences > Security & Privacy
```

### Linux Permission Denied

**Symptoms:**
```
bash: ./cassandra-probe: Permission denied
```

**Solution:**
```bash
# Make executable
chmod +x cassandra-probe

# Check file permissions
ls -la cassandra-probe
```

### Windows Antivirus Blocking

**Symptoms:**
- Executable deleted after download
- "Windows protected your PC" message

**Solutions:**
1. Add exception in Windows Defender
2. Download from GitHub releases directly
3. Verify SHA256 checksum

## Debug Mode

Enable detailed debugging to diagnose issues:

```bash
# Maximum debug output
./cassandra-probe --contact-points cluster:9042 \
  --log-level DEBUG \
  --connection-events \
  --verbose

# Save debug logs
./cassandra-probe --contact-points cluster:9042 \
  --log-level DEBUG \
  --log-file debug.log
```

## Getting Help

If you continue to experience issues:

1. **Check existing issues**: [GitHub Issues](https://github.com/axonops/cassandra-probe-csharp/issues)
2. **Create new issue** with:
   - Cassandra Probe version: `./cassandra-probe --version`
   - Cassandra version: `nodetool version`
   - Full error message
   - Debug logs
   - Configuration used

3. **Community resources**:
   - Apache Cassandra mailing list
   - Cassandra Slack community
   - Stack Overflow tags: `cassandra`, `cassandra-probe`