# Release Notes

## Version 1.0.1 (In Development)

### 🎯 Major Improvements

#### Fixed Critical ID Type Mismatch
- **Issue**: Integer IDs were incorrectly formatted as strings (e.g., `Products('1')` instead of `Products(1)`)
- **Impact**: All GET, UPDATE, and DELETE operations failed with 400 Bad Request
- **Resolution**: Enhanced `FormatKeyValue` to properly handle all OData primitive types

#### Achieved Tool Parity with Go Implementation
- Both implementations now expose exactly 20 tools for the test OData service
- Removed non-functional search tools that were causing 400 errors
- Function imports (like `GetProductsByRating`) now work correctly

#### Improved Type System Support
- Proper formatting for: Int16, Int32, Int64, Double, Decimal, Boolean, DateTimeOffset
- Correct handling of GUID types with `guid'...'` syntax
- String types properly quoted with single quotes

### 🐛 Bug Fixes

- Fixed GetProductsByRating function execution (was throwing internal errors)
- Removed search functions that don't work with OData V2 services
- Fixed metadata parsing for V2 services with function imports
- Improved error logging and debugging infrastructure

### 📝 Documentation Updates

- Updated README to reflect actual capabilities
- Added Known Issues section for Debug vs Release build difference
- Clarified that search functionality is not available for V2 services
- Added troubleshooting guide for common issues

### 🔧 Technical Improvements

- Added comprehensive debugging infrastructure inspired by MinZ
- Replaced file-based debug logging with proper structured logging
- Added V2-specific metadata parser for better compatibility
- Improved tool name suffix generation for multi-server environments

### ⚠️ Known Issues

- **ARM64 macOS Release Build**: Native ARM64 Release builds fail due to a .NET runtime bug. **SOLVED**: Use x64 builds on Apple Silicon (they run perfectly via Rosetta 2). See [ARM64_BUG_FOUND.md](ARM64_BUG_FOUND.md).
- **Write Operations**: Create/Update operations may fail on read-only demo services
- **Date Format**: .NET uses `/Date(timestamp)/` format vs ISO format (cosmetic difference)

### 🚀 Next Steps

- Report ARM64 bug to .NET team
- Add metadata validation for CRUD operations
- Implement automated test suite
- Add performance benchmarking

### 🎉 Major Discovery

Through collaborative debugging, we discovered that the "Release build issue" is actually a .NET runtime bug specific to ARM64 macOS! The solution is elegantly simple: use x64 builds on Apple Silicon Macs, which run flawlessly through Rosetta 2.

This discovery highlights the importance of:
- Testing across different architectures
- Creative debugging approaches
- The amazing compatibility layer that is Rosetta 2

---

## Version 1.0.0 (Initial Release)

- Initial .NET implementation of OData MCP bridge
- Support for OData V2 and V4 services
- Dynamic tool generation for entity sets
- Basic authentication support
- CSRF token handling for SAP services
- Cross-platform support (Windows, Linux, macOS)