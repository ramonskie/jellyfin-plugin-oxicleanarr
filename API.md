# Jellyfin OxiCleanarr Bridge Plugin - API Documentation

## Overview

The Jellyfin OxiCleanarr Bridge Plugin provides a minimal, focused API for managing symlinks. The plugin is **completely stateless and configuration-free** - all paths are provided via API requests.

## Design Philosophy

**The plugin does ONE thing well: Manage symlinks as instructed**

- ✅ Create symlinks at specified locations
- ✅ Remove symlinks
- ✅ List symlinks in specified directories
- ✅ Explicitly manage directories (optional - directories auto-created as fallback)
- ✅ Health check

**The plugin does NOT:**
- ❌ Store any configuration
- ❌ Remember any paths
- ❌ Create/manage Jellyfin libraries
- ❌ Trigger library scans
- ❌ Manage media metadata
- ❌ Handle business logic

**OxiCleanarr is in complete control** - it knows all paths, manages all state, and orchestrates all operations.

## Authentication

All endpoints except `/status` require authentication using Jellyfin's built-in authentication system.

### Using API Keys (Recommended for OxiCleanarr)

```bash
# Create API key in Jellyfin: Dashboard → API Keys
curl -H "X-Emby-Token: your-api-key-here" http://localhost:8096/api/oxicleanarr/...
```

### Using Session Tokens (For Testing)

```bash
# 1. Authenticate with username/password
curl -X POST http://localhost:8096/Users/AuthenticateByName \
  -H "Content-Type: application/json" \
  -H "X-Emby-Authorization: MediaBrowser Client=\"YourApp\", Device=\"YourDevice\", DeviceId=\"device-id\", Version=\"1.0.0\"" \
  -d '{"Username":"your-user","Pw":"your-pass"}'

# 2. Use returned AccessToken
curl -H "X-Emby-Token: session-token" http://localhost:8096/api/oxicleanarr/...
```

## Endpoints

### GET /api/oxicleanarr/status

Get plugin version.

**Authentication:** None required (public endpoint)

**Response:**
```json
{
  "Version": "2.0.0.0"
}
```

**Example:**
```bash
curl http://localhost:8096/api/oxicleanarr/status
```

---

### POST /api/oxicleanarr/symlinks/add

Create symlinks for media files.

**Authentication:** Required

**Request Body:**
```json
{
  "items": [
    {
      "sourcePath": "/media/movies/Movie Name (2024)/movie.mkv",
      "targetDirectory": "/data/leaving-soon"
    }
  ]
}
```

**Response:**
```json
{
  "Success": true,
  "CreatedSymlinks": [
    "/data/leaving-soon/movie.mkv"
  ],
  "Errors": []
}
```

**Response Codes:**
- `200 OK` - Symlinks created successfully
- `400 Bad Request` - Invalid request (no items provided)
- `401 Unauthorized` - Authentication required

**Example:**
```bash
curl -X POST http://localhost:8096/api/oxicleanarr/symlinks/add \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-token" \
  -d '{
    "items": [
      {
        "sourcePath": "/media/movies/Movie1/movie1.mkv",
        "targetDirectory": "/data/leaving-soon"
      },
      {
        "sourcePath": "/media/movies/Movie2/movie2.mkv",
        "targetDirectory": "/data/leaving-soon-tv"
      }
    ]
  }'
```

**Notes:**
- **targetDirectory** is required for each item - OxiCleanarr specifies where to create the symlink
- If symlink already exists, it will be replaced
- Target directory is created automatically if it doesn't exist
- Each item is processed independently - partial success is possible
- Check `Errors` array for any failures

---

### POST /api/oxicleanarr/symlinks/remove

Remove symlinks.

**Authentication:** Required

**Request Body:**
```json
{
  "symlinkPaths": [
    "/data/leaving-soon/movie.mkv"
  ]
}
```

**Response:**
```json
{
  "Success": true,
  "RemovedSymlinks": [
    "/data/leaving-soon/movie.mkv"
  ],
  "Errors": []
}
```

**Response Codes:**
- `200 OK` - Symlinks removed successfully
- `400 Bad Request` - Invalid request (no paths provided)
- `401 Unauthorized` - Authentication required

**Example:**
```bash
curl -X POST http://localhost:8096/api/oxicleanarr/symlinks/remove \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-token" \
  -d '{
    "symlinkPaths": [
      "/data/leaving-soon/movie1.mkv",
      "/data/leaving-soon/movie2.mkv"
    ]
  }'
```

**Notes:**
- If symlink doesn't exist, operation continues without error
- Each path is processed independently
- Check `Errors` array for any failures

---

### GET /api/oxicleanarr/symlinks/list

List all symlinks in a specified directory.

**Authentication:** Required

**Query Parameters:**
- `directory` (required) - The directory path to list symlinks from

**Response:**
```json
{
  "Symlinks": [
    {
      "Path": "/data/leaving-soon/movie.mkv",
      "Target": "/media/movies/Movie Name (2024)/movie.mkv",
      "Name": "movie.mkv"
    }
  ],
  "Count": 1
}
```

**Response Codes:**
- `200 OK` - Symlinks listed successfully
- `400 Bad Request` - Directory parameter missing
- `401 Unauthorized` - Authentication required
- `500 Internal Server Error` - Failed to list symlinks

**Example:**
```bash
curl "http://localhost:8096/api/oxicleanarr/symlinks/list?directory=/data/leaving-soon" \
  -H "X-Emby-Token: your-token"
```

**Notes:**
- **directory** parameter is required - OxiCleanarr specifies which directory to list
- Only returns actual symlinks (not regular files)
- Returns empty array if directory doesn't exist or is empty
- Useful for OxiCleanarr to verify current state

---

### POST /api/oxicleanarr/directories/create

Create a directory explicitly.

**Authentication:** Required

**Request Body:**
```json
{
  "directory": "/data/leaving-soon"
}
```

**Response:**
```json
{
  "Success": true,
  "Directory": "/data/leaving-soon",
  "Created": true,
  "Message": "Directory created successfully"
}
```

**Response Codes:**
- `200 OK` - Directory created or already exists
- `400 Bad Request` - Invalid request (directory path missing)
- `401 Unauthorized` - Authentication required
- `500 Internal Server Error` - Failed to create directory

**Example:**
```bash
curl -X POST http://localhost:8096/api/oxicleanarr/directories/create \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-token" \
  -d '{
    "directory": "/data/leaving-soon"
  }'
```

**Notes:**
- **directory** parameter is required - full path to directory to create
- If directory already exists, returns success with `Created: false`
- Parent directories are created automatically if needed
- This endpoint is optional - directories are auto-created during symlink creation as a fallback
- Useful for OxiCleanarr to pre-create directories before adding symlinks

---

### DELETE /api/oxicleanarr/directories/remove

Remove a directory.

**Authentication:** Required

**Request Body:**
```json
{
  "directory": "/data/leaving-soon",
  "force": false
}
```

**Response:**
```json
{
  "Success": true,
  "Directory": "/data/leaving-soon",
  "Message": "Directory removed successfully"
}
```

**Response Codes:**
- `200 OK` - Directory removed successfully
- `400 Bad Request` - Invalid request or directory not empty (when force=false)
- `401 Unauthorized` - Authentication required
- `500 Internal Server Error` - Failed to remove directory

**Example:**
```bash
# Remove empty directory
curl -X DELETE http://localhost:8096/api/oxicleanarr/directories/remove \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-token" \
  -d '{
    "directory": "/data/leaving-soon",
    "force": false
  }'

# Force remove non-empty directory
curl -X DELETE http://localhost:8096/api/oxicleanarr/directories/remove \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: your-token" \
  -d '{
    "directory": "/data/leaving-soon",
    "force": true
  }'
```

**Notes:**
- **directory** parameter is required - full path to directory to remove
- **force** parameter is optional (defaults to false)
  - `force: false` - Only removes empty directories (safer)
  - `force: true` - Removes directory and all contents recursively
- If directory doesn't exist, returns success (idempotent)
- Use with caution when `force: true` - all contents will be permanently deleted

---

## Configuration

**The plugin requires NO configuration!** It is completely stateless.

All paths are provided via API requests. OxiCleanarr is in complete control of where symlinks are created.

---

## OxiCleanarr Integration Guide

### Recommended Workflow

```python
# 1. (Optional) Explicitly create directory via plugin
# Note: Directories are auto-created during symlink creation as fallback
response = requests.post(
    "http://jellyfin:8096/api/oxicleanarr/directories/create",
    headers={"X-Emby-Token": api_key},
    json={"directory": "/data/leaving-soon"}
)

# 2. Create symlink via plugin
response = requests.post(
    "http://jellyfin:8096/api/oxicleanarr/symlinks/add",
    headers={"X-Emby-Token": api_key},
    json={"items": [{
        "sourcePath": movie_path,
        "targetDirectory": "/data/leaving-soon"
    }]}
)

if not response.json()["Success"]:
    handle_error()

# 3. Ensure library exists (OxiCleanarr's responsibility)
if not library_exists("Leaving Soon"):
    create_library("Leaving Soon", "/data/leaving-soon")

# 4. Trigger library scan (OxiCleanarr's responsibility)
scan_library("Leaving Soon")
```

### Creating the Library (One-Time Setup)

OxiCleanarr should create the "Leaving Soon" library on first use:

```bash
# Via Jellyfin API
POST /Library/VirtualFolders
{
  "name": "Leaving Soon",
  "collectionType": "movies",
  "paths": ["/data/leaving-soon"]
}
```

### Triggering Library Scan

After adding/removing symlinks, OxiCleanarr should trigger a scan:

```bash
# Scan specific library
POST /Library/Refresh
```

---

## Migration from v1.0.2

### Breaking Changes

**Removed Endpoints:**
- `POST /api/oxicleanarr/leaving-soon/clear` - Use `/symlinks/remove` for each item instead

**Renamed Endpoints:**
- `POST /api/oxicleanarr/leaving-soon/add` → `POST /api/oxicleanarr/symlinks/add`
- `POST /api/oxicleanarr/leaving-soon/remove` → `POST /api/oxicleanarr/symlinks/remove`

**Removed Configuration:**
- `AutoCreateVirtualFolder` - OxiCleanarr must create library
- `VirtualFolderName` - OxiCleanarr manages library name
- `SymlinkBasePath` - Target directory now provided in each API request

**Removed Behavior:**
- Plugin no longer creates or manages Jellyfin libraries
- Plugin no longer triggers library scans
- OxiCleanarr is now responsible for library lifecycle

### Migration Steps

1. Update API endpoint URLs in OxiCleanarr
2. Implement library management in OxiCleanarr
3. Implement library scanning in OxiCleanarr after batch operations
4. Remove old configuration options

---

## Error Handling

All endpoints return errors in a consistent format:

**Success with Partial Errors:**
```json
{
  "Success": true,
  "CreatedSymlinks": ["/data/leaving-soon/movie1.mkv"],
  "Errors": [
    "/media/movies/movie2.mkv: File not found"
  ]
}
```

**Complete Failure:**
```json
{
  "error": "Plugin configuration not available"
}
```

**Best Practices:**
- Always check `Success` field
- Check `Errors` array for partial failures
- Log all errors for debugging
- Implement retry logic for transient failures

---

## Testing

See test scripts for examples:
- `tests/test-api.sh` - Tests add and list operations
- `tests/test-remove.sh` - Tests remove and list operations

Run tests:
```bash
# Full test suite
./tests/test-api.sh
./tests/test-remove.sh
```

---

## Technical Details

### Symlink Management

- Uses .NET's `File.CreateSymbolicLink()` (requires .NET 6+)
- Symlinks are created with same filename as source
- Existing symlinks are replaced automatically
- Only actual symlinks are listed (regular files ignored)

### Thread Safety

- All operations are thread-safe
- Multiple concurrent requests are handled correctly
- File operations are atomic where possible

### Platform Support

- **Linux:** Full support ✅
- **Windows:** Requires admin privileges for symlink creation
- **macOS:** Full support ✅

---

## Support

For issues or questions:
- Check logs: `docker logs jellyfin-test -f`
- Verify plugin is loaded: `GET /api/oxicleanarr/status`
- Check authentication: Ensure valid API key or session token
- Review filesystem permissions: Plugin runs as Jellyfin user
