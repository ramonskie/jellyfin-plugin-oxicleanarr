# Installation Guide: Jellyfin Plugin for Prunarr

This guide explains how to install and configure the Jellyfin.Plugin.PrunarrBridge plugin.

## Overview

This native Jellyfin plugin embeds symlink management functionality directly into Jellyfin. No additional containers are required - the plugin runs inside Jellyfin's process and exposes REST endpoints on Jellyfin's port.

## Prerequisites

- Jellyfin server (version 10.8.0 or newer)
- .NET SDK 7.0 or newer (for building the plugin)
- Access to Jellyfin's plugin directory
- Prunarr configured and running

## Building the Plugin

### 1. Install .NET SDK

If you don't have the .NET SDK installed:

```bash
# Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 7.0

# Or use your package manager
apt-get install dotnet-sdk-7.0
```

### 2. Build the Plugin

```bash
cd Jellyfin.Plugin.PrunarrBridge
dotnet restore
dotnet build --configuration Release
```

The compiled plugin will be located at:
```
bin/Release/net7.0/Jellyfin.Plugin.PrunarrBridge.dll
```

## Installation

### Method 1: Manual Installation (Docker)

1. **Locate your Jellyfin config directory** (usually mounted as a volume in Docker)

2. **Create the plugin directory**:
   ```bash
   mkdir -p /path/to/jellyfin/config/plugins/PrunarrBridge
   ```

3. **Copy the plugin DLL**:
   ```bash
   cp bin/Release/net7.0/Jellyfin.Plugin.PrunarrBridge.dll \
      /path/to/jellyfin/config/plugins/PrunarrBridge/
   ```

4. **Restart Jellyfin**:
   ```bash
   docker restart jellyfin
   ```

### Method 2: Manual Installation (Bare Metal)

1. **Locate Jellyfin's plugin directory**:
   - Linux: `/var/lib/jellyfin/plugins/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\`
   - macOS: `/var/lib/jellyfin/plugins/`

2. **Create the plugin directory**:
   ```bash
   mkdir -p /var/lib/jellyfin/plugins/PrunarrBridge
   ```

3. **Copy the plugin DLL**:
   ```bash
   cp bin/Release/net7.0/Jellyfin.Plugin.PrunarrBridge.dll \
      /var/lib/jellyfin/plugins/PrunarrBridge/
   ```

4. **Restart Jellyfin service**:
   ```bash
   sudo systemctl restart jellyfin
   ```

## Configuration

### No Configuration Required! (v2.0+)

The plugin is **completely stateless** and requires no configuration. All paths are provided via API requests.

### Verify Installation

1. Log into Jellyfin as an administrator
2. Navigate to **Dashboard** → **Plugins**
3. Find **Prunarr Bridge** in the plugin list to confirm it's loaded

Check Jellyfin logs for any errors:
```bash
# Docker
docker logs jellyfin

# Systemd
journalctl -u jellyfin -f
```

## Integration with Prunarr

### Configure Prunarr to Use the Plugin

Update Prunarr's configuration to point to the plugin endpoints:

**Base URL**: `http://jellyfin:8096/api/prunarr` (or your Jellyfin URL)

**API Endpoints**:
- Add symlinks: `POST /api/prunarr/symlinks/add`
- Remove symlinks: `POST /api/prunarr/symlinks/remove`
- List symlinks: `GET /api/prunarr/symlinks/list?directory=<path>`
- Status: `GET /api/prunarr/status`

**Authentication**: Use Jellyfin's built-in authentication with `X-Emby-Token` header

### Example API Requests

**Create Symlinks:**
```bash
curl -X POST http://jellyfin:8096/api/prunarr/symlinks/add \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-jellyfin-api-token" \
  -d '{
    "items": [
      {
        "sourcePath": "/media/movies/Movie (2023)/Movie (2023).mkv",
        "targetDirectory": "/data/leaving-soon"
      }
    ]
  }'
```

**List Symlinks:**
```bash
curl "http://jellyfin:8096/api/prunarr/symlinks/list?directory=/data/leaving-soon" \
  -H "X-Emby-Token: your-jellyfin-api-token"
```

**Remove Symlinks:**
```bash
curl -X POST http://jellyfin:8096/api/prunarr/symlinks/remove \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-jellyfin-api-token" \
  -d '{
    "symlinkPaths": [
      "/data/leaving-soon/Movie (2023).mkv"
    ]
  }'
```

For complete API documentation, see [API.md](API.md).

## Volume Mapping Considerations

### Docker Setup

When using Docker, ensure proper volume mapping:

```yaml
services:
  jellyfin:
    volumes:
      # Config directory (where plugin is installed)
      - ./jellyfin-config:/config
      # Your actual media
      - /path/to/movies:/media/movies:ro
      - /path/to/tvshows:/media/tvshows:ro
      # Directory for symlinks (must be writable)
      - leaving-soon-data:/data/leaving-soon
```

**Important**: The target directory paths in API requests must match the path as Jellyfin sees it inside the container (e.g., `/data/leaving-soon`, not the host path).

## Verification

### 1. Check Plugin Status

```bash
curl http://jellyfin:8096/api/prunarr/status
```

Expected response:
```json
{
  "Version": "2.0.0.0"
}
```

### 2. Create Jellyfin API Token

The plugin uses Jellyfin's built-in authentication:

1. Go to **Dashboard** → **API Keys**
2. Click **+** to create a new API key
3. Give it a name (e.g., "Prunarr")
4. Copy the generated token

### 3. Test Adding a Symlink

```bash
curl -X POST http://jellyfin:8096/api/prunarr/symlinks/add \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-token-here" \
  -d '{
    "items": [
      {
        "sourcePath": "/media/movies/TestMovie/TestMovie.mkv",
        "targetDirectory": "/data/leaving-soon"
      }
    ]
  }'
```

Expected response:
```json
{
  "Success": true,
  "CreatedSymlinks": ["/data/leaving-soon/TestMovie.mkv"],
  "Errors": []
}
```

### 4. Verify Symlink Creation

Check that the symlink was created:
```bash
ls -lah /data/leaving-soon/
```

### 5. Create Library in Jellyfin (One-Time)

The plugin does NOT create libraries. You or Prunarr must create the library:

1. Navigate to **Dashboard** → **Libraries**
2. Click **Add Media Library**
3. Name: "Leaving Soon" (or your choice)
4. Content type: Movies (or Mixed)
5. Add folder: `/data/leaving-soon`
6. Save

After creating the library, trigger a scan to see the symlinked media.

## Troubleshooting

### Plugin Not Loading

- Check Jellyfin logs for errors
- Verify the plugin DLL is in the correct directory
- Ensure .NET 7.0 runtime is available
- Try restarting Jellyfin

### Symlink Creation Fails

- Verify write permissions on the symlink base path
- Check that source paths exist and are accessible
- Ensure paths use the correct format (as seen by Jellyfin)

### Library Not Showing Symlinked Media

- Ensure you created the library in Jellyfin pointing to the target directory
- Trigger a library scan after creating symlinks
- Check that the symlinks are valid and point to existing files
- Verify Jellyfin has read permissions on the symlinked files

### API Authentication Fails

- Verify you're using a valid Jellyfin API token
- Check that the `X-Emby-Token` header is being sent correctly
- Ensure the token hasn't been deleted in Jellyfin's API Keys section

## Uninstallation

To remove the plugin:

1. Delete the plugin directory:
   ```bash
   rm -rf /path/to/jellyfin/config/plugins/PrunarrBridge
   ```

2. Restart Jellyfin

3. Optionally, remove any libraries you created for symlinked media from Jellyfin's UI

## Key Features

- **No additional containers**: Runs directly in Jellyfin's process
- **Native integration**: Standard Jellyfin plugin with REST API
- **Stateless design**: No configuration required (v2.0+)
- **Simple deployment**: One less service to manage
- **Direct filesystem access**: Native symlink creation

## Important Notes

- **Requires building**: Must compile C# code (requires .NET SDK)
- **Updates require restart**: Plugin updates need Jellyfin restart
- **Version compatibility**: Plugin must be compatible with Jellyfin version (10.8.0+)
