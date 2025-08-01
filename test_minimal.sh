#!/bin/bash

# Create a test with minimal service that should work quickly
echo "Testing with minimal config..."

# Use verbose mode to see what's happening
echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}' | \
timeout 10 dotnet run --project src/ODataMcp -- \
  --service https://services.odata.org/V2/Northwind/Northwind.svc/ \
  --verbose \
  --entities "Products" \
  2>&1