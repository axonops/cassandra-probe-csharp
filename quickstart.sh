#!/bin/bash
# Quick start script for testing Cassandra Probe locally
# Supports Cassandra 4.0, 4.1, and 5.0 only (3.x versions are NOT supported)

set -e

echo "🚀 Cassandra Probe Quick Start"
echo "=============================="
echo "Supported versions: Cassandra 4.0+"

# Source the container runtime detection script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/detect-container-runtime.sh"

# Detect container runtime
RUNTIME=$(detect_runtime)
if [ $? -ne 0 ]; then
    echo "❌ No container runtime found. Please install Docker or Podman."
    exit 1
fi

echo "✅ Using container runtime: $RUNTIME"

# Function to wait for Cassandra
wait_for_cassandra() {
    local container=$1
    local port=$2
    echo "⏳ Waiting for Cassandra to start on port $port..."
    
    for i in {1..30}; do
        if $RUNTIME exec $container cqlsh -e "DESC KEYSPACES;" &> /dev/null; then
            echo "✅ Cassandra is ready!"
            return 0
        fi
        echo -n "."
        sleep 2
    done
    
    echo "❌ Timeout waiting for Cassandra"
    return 1
}

# Menu
echo ""
echo "Choose a test scenario:"
echo "1) Single node without authentication (easiest)"
echo "2) Single node with authentication"
echo "3) Multi-node cluster (3 nodes)"
echo "4) Stop all test containers"
echo ""
read -p "Enter choice (1-4): " choice

case $choice in
    1)
        echo "🔧 Starting single node Cassandra without authentication..."
        $RUNTIME run -d --name cassandra-test -p 9042:9042 cassandra:4.1
        wait_for_cassandra cassandra-test 9042
        echo ""
        echo "📋 Test commands:"
        echo "  ./cassandra-probe -cp localhost:9042"
        echo "  ./cassandra-probe -cp localhost:9042 -cql \"SELECT * FROM system.local\""
        echo "  ./cassandra-probe -cp localhost:9042 --all-probes"
        ;;
    
    2)
        echo "🔧 Starting single node Cassandra with authentication..."
        $RUNTIME run -d --name cassandra-auth -p 9043:9042 \
            -e CASSANDRA_AUTHENTICATOR=PasswordAuthenticator \
            -e CASSANDRA_AUTHORIZER=CassandraAuthorizer \
            cassandra:4.1
        
        echo "⏳ Waiting for Cassandra with auth (this takes longer)..."
        sleep 30
        
        # Try to connect with auth
        for i in {1..30}; do
            if $RUNTIME exec cassandra-auth cqlsh -u cassandra -p cassandra -e "DESC KEYSPACES;" &> /dev/null; then
                echo "✅ Cassandra with authentication is ready!"
                break
            fi
            echo -n "."
            sleep 2
        done
        
        echo ""
        echo "📋 Test commands:"
        echo "  ./cassandra-probe -cp localhost:9043 -u cassandra -p cassandra"
        echo "  ./cassandra-probe -cp localhost:9043 -u cassandra -p cassandra -cql \"SELECT * FROM system.local\""
        ;;
    
    3)
        echo "🔧 Starting multi-node cluster..."
        
        # Create network
        $RUNTIME network create cassandra-net 2>/dev/null || true
        
        # Start first node
        echo "Starting node 1..."
        $RUNTIME run -d --name cassandra-node1 --network cassandra-net \
            -p 9044:9042 -e CASSANDRA_CLUSTER_NAME=TestCluster \
            -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
            -e CASSANDRA_DC=dc1 cassandra:4.1
        
        # Wait for first node
        sleep 30
        
        # Start second node
        echo "Starting node 2..."
        $RUNTIME run -d --name cassandra-node2 --network cassandra-net \
            -p 9045:9042 -e CASSANDRA_CLUSTER_NAME=TestCluster \
            -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
            -e CASSANDRA_DC=dc1 -e CASSANDRA_SEEDS=cassandra-node1 cassandra:4.1
        
        # Start third node
        echo "Starting node 3..."
        $RUNTIME run -d --name cassandra-node3 --network cassandra-net \
            -p 9046:9042 -e CASSANDRA_CLUSTER_NAME=TestCluster \
            -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
            -e CASSANDRA_DC=dc1 -e CASSANDRA_SEEDS=cassandra-node1 cassandra:4.1
        
        echo "⏳ Waiting for cluster to form..."
        sleep 45
        
        echo "✅ Multi-node cluster is ready!"
        echo ""
        echo "📋 Test commands:"
        echo "  ./cassandra-probe -cp localhost:9044"
        echo "  ./cassandra-probe -cp localhost:9044,localhost:9045,localhost:9046 --all-probes"
        echo ""
        echo "Check cluster status:"
        echo "  $RUNTIME exec cassandra-node1 nodetool status"
        ;;
    
    4)
        echo "🧹 Stopping all test containers..."
        $RUNTIME stop cassandra-test cassandra-auth cassandra-node1 cassandra-node2 cassandra-node3 2>/dev/null || true
        $RUNTIME rm cassandra-test cassandra-auth cassandra-node1 cassandra-node2 cassandra-node3 2>/dev/null || true
        $RUNTIME network rm cassandra-net 2>/dev/null || true
        echo "✅ All test containers stopped and removed"
        ;;
    
    *)
        echo "❌ Invalid choice"
        exit 1
        ;;
esac

echo ""
echo "🎉 Done! Happy testing!"