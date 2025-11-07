# Jellyfin Plugin for Prunarr

A native Jellyfin plugin that enables Prunarr to manage "Leaving Soon" media visibility through symlinks via HTTP API.

## Problem

Prunarr currently requires direct filesystem access to create symlinks to media files that are approaching deletion. This creates deployment complexity with Docker volume mappings and path translation issues.

## Solution

This Jellyfin plugin moves symlink management into Jellyfin's filesystem context, exposing HTTP APIs that Prunarr can call. The plugin follows the Single Responsibility Principle: it **only manages symlinks**. Library management and scanning remain the responsibility of Prunarr.

## Features

- Native Jellyfin plugin with direct filesystem access
- Exposes REST endpoints on Jellyfin's port (no additional containers needed)
- **Minimal scope:** Only manages symlinks (create, remove, list)
- Completely stateless - all paths provided via API requests
- Uses Jellyfin's built-in authentication
- Prunarr handles library management and scanning

## Installation

### Easy Installation (Recommended)

Add the plugin repository to Jellyfin:

1. Open Jellyfin → **Dashboard** → **Plugins** → **Repositories**
2. Click **"+"** to add a repository
3. Enter:
   - **Repository Name**: `Prunarr Plugin Repository`
   - **Repository URL**: `https://raw.githubusercontent.com/YOUR_USERNAME/jellyfin-plugin-prunarr-bridge/main/manifest.json`
4. Click **Save**
5. Go to **Dashboard** → **Plugins** → **Catalog**
6. Find "Prunarr Bridge" and click **Install**
7. Restart Jellyfin when prompted

### Manual Installation

See [INSTALLATION.md](INSTALLATION.md) for manual installation instructions.

**API Documentation**: See [API.md](API.md)

## Design Philosophy (v2.0+)

The plugin follows the **Single Responsibility Principle**:

**Plugin Responsibilities:**
- ✅ Create symlinks
- ✅ Remove symlinks
- ✅ List symlinks
- ✅ Health check

**Prunarr Responsibilities:**
- ✅ Create/manage Jellyfin libraries
- ✅ Trigger library scans
- ✅ Orchestrate workflow
- ✅ Business logic

## Quick Start

```bash
# 1. Add plugin repository to Jellyfin
# Dashboard → Plugins → Repositories → Add
# URL: https://raw.githubusercontent.com/YOUR_USERNAME/jellyfin-plugin-prunarr-bridge/main/manifest.json

# 2. Install plugin from catalog
# Dashboard → Plugins → Catalog → Find "Prunarr Bridge" → Install

# 3. Restart Jellyfin

# 4. No plugin configuration needed!
# The plugin is completely stateless - all paths are provided via API

# 5. Create a library in Jellyfin (one-time setup)
# Dashboard → Libraries → Add Media Library
# Name: Leaving Soon
# Folder: /data/leaving-soon
# Content type: Movies (or Mixed)

# 6. Test the API
./tests/test-api.sh
```

For manual installation from source, see [INSTALLATION.md](INSTALLATION.md).

## API Endpoints

Complete API documentation: [API.md](API.md)

### Health Check
```bash
GET /api/prunarr/status
```

### Create Symlinks
```bash
POST /api/prunarr/symlinks/add
Content-Type: application/json
X-Emby-Token: your-jellyfin-api-token

{
  "items": [
    {
      "sourcePath": "/media/movies/Movie (2023)/Movie (2023).mkv",
      "targetDirectory": "/data/leaving-soon"
    }
  ]
}
```

### Remove Symlinks
```bash
POST /api/prunarr/symlinks/remove
Content-Type: application/json
X-Emby-Token: your-jellyfin-api-token

{
  "symlinkPaths": ["/data/leaving-soon/Movie (2023).mkv"]
}
```

### List Symlinks
```bash
GET /api/prunarr/symlinks/list?directory=/data/leaving-soon
X-Emby-Token: your-jellyfin-api-token
```

**Authentication**: Uses Jellyfin's built-in authentication. Use any valid Jellyfin API token with the `X-Emby-Token` header.

## Documentation

- **[API.md](API.md)** - Complete API documentation and integration guide
- **[INSTALLATION.md](INSTALLATION.md)** - Plugin installation guide
- **[tests/QUICKSTART_TEST.md](tests/QUICKSTART_TEST.md)** - Quick testing guide
- **[tests/TEST_GUIDE.md](tests/TEST_GUIDE.md)** - Detailed testing documentation

## Benefits

- ✅ Direct filesystem access (no path translation needed)
- ✅ Native Jellyfin integration
- ✅ Simple deployment (no extra containers)
- ✅ Minimal, focused scope (symlinks only)
- ✅ Completely stateless (no configuration required)
- ✅ Production-ready with comprehensive testing

## Project Structure

```
.
├── Jellyfin.Plugin.PrunarrBridge/    # C# Plugin
│   ├── Api/
│   │   └── PrunarrController.cs      # REST API endpoints
│   ├── Configuration/
│   │   ├── configPage.html           # Configuration UI
│   │   └── PluginConfiguration.cs    # Plugin settings
│   ├── Services/
│   │   └── SymlinkManager.cs         # Symlink operations
│   ├── Plugin.cs                     # Plugin entry point
│   └── build.yaml                    # JPRM build config
│
├── tests/                            # Test suite
│   ├── docker-compose.yml            # Test environment
│   ├── test-api.sh                   # API test script
│   ├── test-plugin.sh                # Plugin test script
│   ├── QUICKSTART_TEST.md            # Quick test guide
│   └── TEST_GUIDE.md                 # Detailed testing docs
│
├── API.md                            # API documentation
├── INSTALLATION.md                   # Installation guide
├── PLUGIN_REPOSITORY_SETUP.md        # Plugin repository setup
├── manifest.json                     # Plugin manifest
└── README.md                         # This file
```

## Development Status

- ✅ Plugin v2.0 - Complete (symlink-only design)
- ✅ Comprehensive API documentation
- ✅ Test suite with automated scripts
- ✅ Production-ready
- ⏳ Integration with Prunarr - Pending
- ⏳ Community testing - Pending

## Requirements

- .NET SDK 9.0+
- Jellyfin 10.11.0+
- Linux/macOS (Windows requires admin privileges for symlinks)
- Access to Jellyfin's plugin directory

## Contributing

This is a proof-of-concept for evaluating integration approaches. Once the recommended approach is selected and integrated into Prunarr, contributions will be welcome.

## Architecture

### Before: Direct Filesystem Access (Problem)
```
┌─────────┐     Volume     ┌──────────┐
│ Prunarr │────Mapping─────│ Jellyfin │
└─────────┘                └──────────┘
     │                           │
     │ Create symlinks           │
     │ Path translation issues   │
     └──────────┬────────────────┘
                │
         ┌──────▼──────┐
         │   Media     │
         │ Filesystem  │
         └─────────────┘
```

### After: Plugin API (Solution)
```
┌─────────┐   HTTP API   ┌──────────────────┐
│ Prunarr │─────────────▶│ Jellyfin Plugin  │
└─────────┘              └──────────────────┘
     │                           │
     │                           │ Create symlinks
     │                           │ (native filesystem access)
     │ Create libraries          │
     │ Trigger scans             │
     │                           │
     └──────────┬────────────────┘
                │
         ┌──────▼──────┐
         │  Symlinks   │
         │  Directory  │
         └─────────────┘
```

## License

This is a proof-of-concept project. License to be determined when integrated into Prunarr.

## Related Projects

- **Prunarr** - Media cleanup tool for Plex/Jellyfin/Emby
- **Jellyfin** - Free software media system

## Support

For questions or issues:
1. Review the [API.md](API.md) documentation
2. Check [INSTALLATION.md](INSTALLATION.md) for setup help
3. Review [tests/TEST_GUIDE.md](tests/TEST_GUIDE.md) for troubleshooting
4. Check plugin logs in Jellyfin

## Next Steps

1. **Integration**: Integrate plugin into Prunarr codebase
2. **Testing**: End-to-end testing with real Jellyfin instances
3. **Documentation**: Update Prunarr docs with new deployment method
4. **Community**: Beta testing with Prunarr users
