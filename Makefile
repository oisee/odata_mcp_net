# OData MCP .NET Makefile
.PHONY: all build clean test publish publish-all publish-windows publish-linux publish-macos help

# Variables
PROJECT_NAME = odata-mcp
MAIN_PROJECT = src/ODataMcp/ODataMcp.csproj
OUTPUT_DIR = bin/publish
DIST_DIR = bin/dist
VERSION = 1.0.0

# Default target
all: build

# Build for current platform
build:
	@echo "Building OData MCP for current platform..."
	dotnet build $(MAIN_PROJECT) -c Release

# Clean build artifacts
clean:
	@echo "Cleaning build artifacts..."
	dotnet clean
	rm -rf bin obj $(OUTPUT_DIR) $(DIST_DIR)
	find . -name "bin" -type d -exec rm -rf {} + 2>/dev/null || true
	find . -name "obj" -type d -exec rm -rf {} + 2>/dev/null || true

# Run tests
test:
	@echo "Running tests..."
	dotnet test --logger "console;verbosity=normal"

# Publish for all platforms
publish-all: publish-windows publish-linux publish-macos
	@echo "All platform builds completed!"
	@ls -la $(OUTPUT_DIR)/

# Publish for Windows
publish-windows:
	@echo "Publishing for Windows x64..."
	dotnet publish $(MAIN_PROJECT) -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/win-x64
	@echo "Windows build complete: $(OUTPUT_DIR)/win-x64/$(PROJECT_NAME).exe"

# Publish for Linux
publish-linux:
	@echo "Publishing for Linux x64..."
	dotnet publish $(MAIN_PROJECT) -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/linux-x64
	@chmod +x $(OUTPUT_DIR)/linux-x64/$(PROJECT_NAME)
	@echo "Linux build complete: $(OUTPUT_DIR)/linux-x64/$(PROJECT_NAME)"

# Publish for macOS (both Intel and Apple Silicon)
publish-macos: publish-macos-x64 publish-macos-arm64

publish-macos-x64:
	@echo "Publishing for macOS x64 (Intel)..."
	dotnet publish $(MAIN_PROJECT) -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/osx-x64
	@chmod +x $(OUTPUT_DIR)/osx-x64/$(PROJECT_NAME)
	@echo "macOS Intel build complete: $(OUTPUT_DIR)/osx-x64/$(PROJECT_NAME)"

publish-macos-arm64:
	@echo "⚠️  WARNING: ARM64 Release builds have a known runtime bug on macOS"
	@echo "   Building Debug version for ARM64. For Release, use x64 build (runs via Rosetta 2)"
	@echo "   See ARM64_BUG_FOUND.md for details"
	@echo ""
	@echo "Publishing for macOS ARM64 (Apple Silicon) - Debug mode..."
	dotnet publish $(MAIN_PROJECT) -c Debug -r osx-arm64 --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/osx-arm64-debug
	@chmod +x $(OUTPUT_DIR)/osx-arm64-debug/$(PROJECT_NAME)
	@echo "macOS Apple Silicon Debug build complete: $(OUTPUT_DIR)/osx-arm64-debug/$(PROJECT_NAME)"

# Development build and run
dev: build
	@echo "Running OData MCP..."
	dotnet run --project $(MAIN_PROJECT) -- --help

# Install dependencies
restore:
	@echo "Restoring NuGet packages..."
	dotnet restore

# Run with Northwind example
run-northwind:
	dotnet run --project $(MAIN_PROJECT) -- --service https://services.odata.org/V2/Northwind/Northwind.svc/

# Create distribution packages
dist: publish-all
	@echo "Creating distribution packages..."
	@mkdir -p $(DIST_DIR)
	@echo "Packaging Windows x64..."
	@cd $(OUTPUT_DIR)/win-x64 && zip -r ../../dist/$(PROJECT_NAME)-$(VERSION)-win-x64.zip .
	@echo "Packaging Linux x64..."
	@cd $(OUTPUT_DIR)/linux-x64 && tar -czf ../../dist/$(PROJECT_NAME)-$(VERSION)-linux-x64.tar.gz .
	@echo "Packaging macOS x64..."
	@cd $(OUTPUT_DIR)/osx-x64 && tar -czf ../../dist/$(PROJECT_NAME)-$(VERSION)-osx-x64.tar.gz .
	@echo "Packaging macOS ARM64 Debug..."
	@cd $(OUTPUT_DIR)/osx-arm64-debug && tar -czf ../../dist/$(PROJECT_NAME)-$(VERSION)-osx-arm64-debug.tar.gz .
	@echo ""
	@echo "Distribution packages created:"
	@ls -lh $(DIST_DIR)/
	@echo ""
	@echo "🎉 Release $(VERSION) packages ready!"
	@echo ""
	@echo "⚠️  IMPORTANT: Apple Silicon users should use the x64 build"
	@echo "   due to an ARM64 .NET runtime bug. It runs perfectly via Rosetta 2!"
	@echo "   See ARM64_BUG_FOUND.md for details."

# Install local binary for development
install-local: publish-macos-x64
	@echo "Installing local binary for development (x64 for Apple Silicon compatibility)..."
	@cp $(OUTPUT_DIR)/osx-x64/$(PROJECT_NAME) ./$(PROJECT_NAME)
	@echo "Local binary installed: ./$(PROJECT_NAME)"
	@echo "Note: Using x64 build which runs perfectly on Apple Silicon via Rosetta 2"

# Create a release with all binaries
release: clean dist install-local
	@echo "Release $(VERSION) created successfully!"
	@echo "Files ready for GitHub release:"
	@ls -1 $(DIST_DIR)/

# Help
help:
	@echo "OData MCP .NET Makefile"
	@echo ""
	@echo "Usage: make [target]"
	@echo ""
	@echo "Targets:"
	@echo "  all            - Build for current platform (default)"
	@echo "  build          - Build the project"
	@echo "  clean          - Clean build artifacts"
	@echo "  test           - Run unit tests"
	@echo "  publish-all    - Publish for all platforms"
	@echo "  publish-windows - Publish for Windows x64"
	@echo "  publish-linux  - Publish for Linux x64"
	@echo "  publish-macos  - Publish for macOS (Intel and Apple Silicon)"
	@echo "  dist           - Create distribution packages for all platforms"
	@echo "  release        - Clean build and create release packages"
	@echo "  dev            - Build and show help"
	@echo "  restore        - Restore NuGet packages"
	@echo "  run-northwind  - Run with Northwind test service"
	@echo "  help           - Show this help message"
	@echo "  install-local  - Install binary to project root for Claude Desktop"