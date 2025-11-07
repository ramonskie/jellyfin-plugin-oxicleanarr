# Plugin Repository Setup Guide

This guide explains how to set up a Jellyfin plugin repository so users can install the Prunarr Bridge plugin directly from Jellyfin's plugin catalog.

> **Note for Plugin Maintainers**: This setup is required to enable the easy installation method described in README.md. Once configured, users can install the plugin by simply adding your repository URL to Jellyfin.

## Overview

Jellyfin uses a repository system for plugins. A repository is simply a JSON manifest file hosted on a web server (typically GitHub) that describes available plugins and their versions.

Once set up, users can install your plugin by adding this URL to Jellyfin:
```
https://raw.githubusercontent.com/YOUR_USERNAME/jellyfin-plugin-prunarr-bridge/main/manifest.json
```

## Repository Structure

```
your-github-repo/
├── manifest.json                          # Plugin repository manifest
├── .github/workflows/build-release.yml    # Automated builds
├── build.sh                               # Manual build script
├── Jellyfin.Plugin.PrunarrBridge/         # Plugin source code
└── README.md
```

## Step-by-Step Setup

### 1. Create GitHub Repository

1. Create a new GitHub repository (e.g., `jellyfin-plugin-prunarr-bridge`)
2. Push this code to the repository:

```bash
cd /path/to/test-jelly-plug
git init
git add .
git commit -m "Initial commit: Jellyfin Prunarr Bridge plugin"
git remote add origin https://github.com/ramonskie/jellyfin-plugin-prunarr-bridge.git
git push -u origin main
```

### 2. Build and Create First Release

#### Option A: Using GitHub Actions (Recommended)

1. Create a version tag:
```bash
git tag v1.0.0
git push origin v1.0.0
```

2. GitHub Actions will automatically:
   - Build the plugin DLL
   - Create a ZIP file
   - Generate checksums
   - Create a GitHub release
   - Attach the artifacts

#### Option B: Manual Build

1. Run the build script:
```bash
./build.sh 1.0.0
```

2. Create a GitHub release manually:
   - Go to your repository on GitHub
   - Click "Releases" → "Create a new release"
   - Tag: `v1.0.0`
   - Title: `Prunarr Bridge v1.0.0`
   - Upload `jellyfin-plugin-prunarr-bridge.zip`

### 3. Update Manifest with Release URL

After creating the release, update `manifest.json`:

1. Copy the direct download URL from the GitHub release:
   ```
   https://github.com/ramonskie/jellyfin-plugin-prunarr-bridge/releases/download/v1.0.0/jellyfin-plugin-prunarr-bridge.zip
   ```

2. Get the MD5 checksum from the `.md5` file

3. Update `manifest.json`:

```json
{
  "versions": [
    {
      "version": "1.0.0.0",
      "sourceUrl": "https://github.com/ramonskie/jellyfin-plugin-prunarr-bridge/releases/download/v1.0.0/jellyfin-plugin-prunarr-bridge.zip",
      "checksum": "YOUR_ACTUAL_MD5_CHECKSUM_HERE",
      "timestamp": "2024-01-15T10:00:00Z"
    }
  ]
}
```

4. Commit and push the updated manifest:
```bash
git add manifest.json
git commit -m "Update manifest with v1.0.0 release"
git push
```

### 4. Test the Repository

Get the raw URL for your manifest:
```
https://raw.githubusercontent.com/ramonskie/jellyfin-plugin-prunarr-bridge/main/manifest.json
```

Test it works by visiting the URL in your browser. You should see the JSON manifest.

## Installing the Plugin from Repository

### Adding Repository to Jellyfin

1. Open Jellyfin web interface
2. Navigate to **Dashboard** → **Plugins** → **Repositories**
3. Click the **"+"** button
4. Enter:
   - **Repository Name**: `Prunarr Plugin Repository`
   - **Repository URL**: `https://raw.githubusercontent.com/ramonskie/jellyfin-plugin-prunarr-bridge/main/manifest.json`
5. Click **Save**

### Installing the Plugin

1. Go to **Dashboard** → **Plugins** → **Catalog**
2. Find "Prunarr Bridge" in the list
3. Click **Install**
4. Restart Jellyfin when prompted
5. Configure the plugin in **Dashboard** → **Plugins** → **Prunarr Bridge**

## Repository URL for Users

Give users this URL to add to their Jellyfin:

```
https://raw.githubusercontent.com/ramonskie/jellyfin-plugin-prunarr-bridge/main/manifest.json
```

Users add it via: **Dashboard** → **Plugins** → **Repositories** → **Add**

## Releasing New Versions

### Automated Release (GitHub Actions)

1. Update version in `Jellyfin.Plugin.PrunarrBridge.csproj`:
```xml
<AssemblyVersion>1.1.0.0</AssemblyVersion>
<FileVersion>1.1.0.0</FileVersion>
<Version>1.1.0</Version>
```

2. Commit the changes:
```bash
git add Jellyfin.Plugin.PrunarrBridge/Jellyfin.Plugin.PrunarrBridge.csproj
git commit -m "Bump version to 1.1.0"
git push
```

3. Create and push a new tag:
```bash
git tag v1.1.0
git push origin v1.1.0
```

4. GitHub Actions will automatically build and create the release

5. Update `manifest.json` with new version entry:
```json
{
  "versions": [
    {
      "version": "1.1.0.0",
      "changelog": "What's new in this version",
      "targetAbi": "10.8.0.0",
      "sourceUrl": "https://github.com/ramonskie/jellyfin-plugin-prunarr-bridge/releases/download/v1.1.0/jellyfin-plugin-prunarr-bridge.zip",
      "checksum": "NEW_MD5_CHECKSUM",
      "timestamp": "2024-02-01T10:00:00Z"
    },
    {
      "version": "1.0.0.0",
      ...previous version...
    }
  ]
}
```

6. Commit and push manifest:
```bash
git add manifest.json
git commit -m "Add v1.1.0 to manifest"
git push
```

### Manual Release

1. Update version numbers
2. Run `./build.sh 1.1.0`
3. Create GitHub release manually
4. Update manifest.json
5. Push changes

## Manifest File Structure Explained

```json
[
  {
    "guid": "a8c0f8e4-7d3c-4b5a-9e2f-1a2b3c4d5e6f",  // Must match Plugin.cs
    "name": "Prunarr Bridge",                        // Display name
    "description": "...",                            // Short description
    "overview": "...",                               // One-liner
    "owner": "prunarr",                              // Owner/org
    "category": "General",                           // Category in catalog
    "versions": [                                    // Array of versions
      {
        "version": "1.0.0.0",                       // Version number
        "changelog": "...",                          // Release notes
        "targetAbi": "10.8.0.0",                    // Min Jellyfin version
        "sourceUrl": "https://...",                 // Direct download URL
        "checksum": "md5hash",                      // MD5 checksum
        "timestamp": "2024-01-01T00:00:00Z"        // Release date (ISO 8601)
      }
    ]
  }
]
```

### Important Notes

- **GUID**: Must match exactly with `Plugin.cs` (`Id` property)
- **Version Format**: Use 4-part version: `major.minor.patch.build`
- **targetAbi**: Minimum Jellyfin version (use `10.8.0.0` for Jellyfin 10.8+)
- **sourceUrl**: Must be direct download link (not the release page)
- **checksum**: MD5 hash of the ZIP file (Jellyfin uses this to verify downloads)

## Getting the MD5 Checksum

### From GitHub Actions

The workflow automatically generates checksums. Download the `.md5` file from the release artifacts.

### Manually

```bash
md5sum jellyfin-plugin-prunarr-bridge.zip
# Output: abc123def456... jellyfin-plugin-prunarr-bridge.zip
# Use the hash part (before the filename)
```

Or on macOS:
```bash
md5 jellyfin-plugin-prunarr-bridge.zip
```

## Troubleshooting

### Plugin Doesn't Appear in Catalog

**Problem**: Added repository but plugin doesn't show up

**Solutions**:
- Check manifest URL is correct (raw GitHub URL)
- Verify manifest.json is valid JSON (use JSONLint.com)
- Check browser console for errors
- Try refreshing the plugin catalog
- Restart Jellyfin

### Download Fails

**Problem**: "Failed to download plugin" error

**Solutions**:
- Verify sourceUrl points directly to the ZIP file
- Check the file is publicly accessible (try downloading manually)
- Verify checksum matches the actual file
- Ensure ZIP contains the DLL files

### Version Doesn't Show Up

**Problem**: New version not appearing in Jellyfin

**Solutions**:
- Verify manifest.json is updated on GitHub
- Check manifest URL points to the correct branch (usually `main`)
- Clear browser cache
- Wait a few minutes (GitHub CDN cache)
- Restart Jellyfin

### GUID Mismatch

**Problem**: "Plugin GUID mismatch" error

**Solution**: Ensure GUID in manifest.json matches Plugin.cs:

```csharp
// Plugin.cs
public override Guid Id => Guid.Parse("a8c0f8e4-7d3c-4b5a-9e2f-1a2b3c4d5e6f");
```

```json
// manifest.json
"guid": "a8c0f8e4-7d3c-4b5a-9e2f-1a2b3c4d5e6f"
```

## Testing Changes Locally

Before pushing to GitHub, test the manifest locally:

1. Host manifest.json on a local web server:
```bash
python3 -m http.server 8000
```

2. Add repository in Jellyfin:
```
http://localhost:8000/manifest.json
```

3. Test installation

## Security Considerations

### Repository Security

- Keep your repository public (Jellyfin needs to access manifest.json)
- Use GitHub releases for hosting plugin files (reliable and fast)
- Include checksums to ensure file integrity

### Plugin Security

- Code review all changes
- Don't include sensitive information in the plugin
- Use API keys for authentication (not hardcoded credentials)
- Validate all input from HTTP endpoints

## Distribution Options

### Option 1: GitHub Releases (Recommended)

**Pros**:
- Free and reliable hosting
- Automatic versioning
- Built-in checksums
- CDN distribution

**Cons**:
- Requires GitHub account
- Manual manifest updates

### Option 2: Official Jellyfin Repository

Submit to the official Jellyfin plugin repository:

1. Fork https://github.com/jellyfin/jellyfin-plugin-repository
2. Add your plugin to their manifest
3. Submit a pull request

**Pros**:
- Appears in official catalog
- Wider distribution
- Community trust

**Cons**:
- Requires code review
- Stricter guidelines
- Slower approval process

### Option 3: Self-Hosted

Host manifest.json on your own server:

**Pros**:
- Full control
- No GitHub dependency

**Cons**:
- Need web server with HTTPS
- Reliability concerns
- No built-in checksums

## Example Workflow

### For Users

1. Add repository URL to Jellyfin
2. Install plugin from catalog
3. Restart Jellyfin
4. Configure plugin settings
5. Use with Prunarr

### For Developers (Releasing Updates)

1. Make code changes
2. Update version in `.csproj`
3. Commit and push
4. Create version tag: `git tag v1.x.x`
5. Push tag: `git push origin v1.x.x`
6. GitHub Actions builds and releases automatically
7. Update manifest.json with new version
8. Push manifest changes
9. Users see update in Jellyfin plugin manager

## Repository URL Template

Give users this template for their documentation:

```markdown
## Installing Prunarr Bridge Plugin

1. Open Jellyfin → Dashboard → Plugins → Repositories
2. Click "+" to add a repository
3. Enter:
   - Name: `Prunarr Plugin Repository`
   - URL: `https://raw.githubusercontent.com/ramonskie/jellyfin-plugin-prunarr-bridge/main/manifest.json`
4. Save
5. Go to Dashboard → Plugins → Catalog
6. Find "Prunarr Bridge" and click Install
7. Restart Jellyfin
8. Configure in Dashboard → Plugins → Prunarr Bridge
```

## Next Steps

1. Replace `ramonskie` in all files with your actual GitHub username
2. Create the GitHub repository
3. Build and release v1.0.0
4. Update manifest.json with real URLs and checksums
5. Test installation from Jellyfin
6. Document the repository URL for users

## Additional Resources

- [Jellyfin Plugin Documentation](https://jellyfin.org/docs/general/server/plugins/)
- [Example Plugin Repositories](https://github.com/jellyfin/jellyfin-plugin-repository)
- [Plugin Development Guide](https://github.com/jellyfin/jellyfin-plugin-template)
