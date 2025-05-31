# Quick start script for testing Cassandra Probe locally on Windows
# Supports Cassandra 4.0, 4.1, and 5.0 only (3.x versions are NOT supported)
# Run with: powershell -ExecutionPolicy Bypass -File quickstart.ps1

Write-Host "🚀 Cassandra Probe Quick Start" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green
Write-Host "Supported versions: Cassandra 4.0+" -ForegroundColor Yellow

# Check if Docker is installed
try {
    docker --version | Out-Null
    Write-Host "✅ Docker is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker is required but not installed. Please install Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Check if Docker is running
try {
    docker info | Out-Null
    Write-Host "✅ Docker is running" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Function to wait for Cassandra
function Wait-ForCassandra {
    param($Container, $Port)
    
    Write-Host "⏳ Waiting for Cassandra to start on port $Port..." -ForegroundColor Yellow
    
    for ($i = 1; $i -le 30; $i++) {
        try {
            docker exec $Container cqlsh -e "DESC KEYSPACES;" 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Cassandra is ready!" -ForegroundColor Green
                return $true
            }
        } catch {}
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 2
    }
    
    Write-Host "❌ Timeout waiting for Cassandra" -ForegroundColor Red
    return $false
}

# Menu
Write-Host ""
Write-Host "Choose a test scenario:"
Write-Host "1) Single node without authentication (easiest)"
Write-Host "2) Single node with authentication"
Write-Host "3) Multi-node cluster (3 nodes)"
Write-Host "4) Stop all test containers"
Write-Host ""
$choice = Read-Host "Enter choice (1-4)"

switch ($choice) {
    "1" {
        Write-Host "🔧 Starting single node Cassandra without authentication..." -ForegroundColor Yellow
        docker run -d --name cassandra-test -p 9042:9042 cassandra:4.1
        
        if (Wait-ForCassandra -Container "cassandra-test" -Port 9042) {
            Write-Host ""
            Write-Host "📋 Test commands:" -ForegroundColor Cyan
            Write-Host "  .\cassandra-probe.exe -cp localhost:9042"
            Write-Host "  .\cassandra-probe.exe -cp localhost:9042 -cql `"SELECT * FROM system.local`""
            Write-Host "  .\cassandra-probe.exe -cp localhost:9042 --all-probes"
        }
    }
    
    "2" {
        Write-Host "🔧 Starting single node Cassandra with authentication..." -ForegroundColor Yellow
        docker run -d --name cassandra-auth -p 9043:9042 `
            -e CASSANDRA_AUTHENTICATOR=PasswordAuthenticator `
            -e CASSANDRA_AUTHORIZER=CassandraAuthorizer `
            cassandra:4.1
        
        Write-Host "⏳ Waiting for Cassandra with auth (this takes longer)..." -ForegroundColor Yellow
        Start-Sleep -Seconds 30
        
        # Try to connect with auth
        for ($i = 1; $i -le 30; $i++) {
            try {
                docker exec cassandra-auth cqlsh -u cassandra -p cassandra -e "DESC KEYSPACES;" 2>$null | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ Cassandra with authentication is ready!" -ForegroundColor Green
                    break
                }
            } catch {}
            Write-Host "." -NoNewline
            Start-Sleep -Seconds 2
        }
        
        Write-Host ""
        Write-Host "📋 Test commands:" -ForegroundColor Cyan
        Write-Host "  .\cassandra-probe.exe -cp localhost:9043 -u cassandra -p cassandra"
        Write-Host "  .\cassandra-probe.exe -cp localhost:9043 -u cassandra -p cassandra -cql `"SELECT * FROM system.local`""
    }
    
    "3" {
        Write-Host "🔧 Starting multi-node cluster..." -ForegroundColor Yellow
        
        # Create network
        docker network create cassandra-net 2>$null | Out-Null
        
        # Start first node
        Write-Host "Starting node 1..."
        docker run -d --name cassandra-node1 --network cassandra-net `
            -p 9044:9042 -e CASSANDRA_CLUSTER_NAME=TestCluster `
            -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch `
            -e CASSANDRA_DC=dc1 cassandra:4.1
        
        # Wait for first node
        Start-Sleep -Seconds 30
        
        # Start second node
        Write-Host "Starting node 2..."
        docker run -d --name cassandra-node2 --network cassandra-net `
            -p 9045:9042 -e CASSANDRA_CLUSTER_NAME=TestCluster `
            -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch `
            -e CASSANDRA_DC=dc1 -e CASSANDRA_SEEDS=cassandra-node1 cassandra:4.1
        
        # Start third node
        Write-Host "Starting node 3..."
        docker run -d --name cassandra-node3 --network cassandra-net `
            -p 9046:9042 -e CASSANDRA_CLUSTER_NAME=TestCluster `
            -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch `
            -e CASSANDRA_DC=dc1 -e CASSANDRA_SEEDS=cassandra-node1 cassandra:4.1
        
        Write-Host "⏳ Waiting for cluster to form..." -ForegroundColor Yellow
        Start-Sleep -Seconds 45
        
        Write-Host "✅ Multi-node cluster is ready!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📋 Test commands:" -ForegroundColor Cyan
        Write-Host "  .\cassandra-probe.exe -cp localhost:9044"
        Write-Host "  .\cassandra-probe.exe -cp localhost:9044,localhost:9045,localhost:9046 --all-probes"
        Write-Host ""
        Write-Host "Check cluster status:"
        Write-Host "  docker exec cassandra-node1 nodetool status"
    }
    
    "4" {
        Write-Host "🧹 Stopping all test containers..." -ForegroundColor Yellow
        docker stop cassandra-test cassandra-auth cassandra-node1 cassandra-node2 cassandra-node3 2>$null | Out-Null
        docker rm cassandra-test cassandra-auth cassandra-node1 cassandra-node2 cassandra-node3 2>$null | Out-Null
        docker network rm cassandra-net 2>$null | Out-Null
        Write-Host "✅ All test containers stopped and removed" -ForegroundColor Green
    }
    
    default {
        Write-Host "❌ Invalid choice" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "🎉 Done! Happy testing!" -ForegroundColor Green