using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OxiCleanarr.Services;

/// <summary>
/// Service for managing symlinks within Jellyfin's filesystem context.
/// </summary>
public class SymlinkManager
{
    private readonly ILogger<SymlinkManager> _logger;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymlinkManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="libraryManager">The library manager.</param>
    public SymlinkManager(ILogger<SymlinkManager> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="directoryPath">The directory path to ensure exists.</param>
    /// <returns>True if directory was created, false if it already existed.</returns>
    public bool EnsureDirectoryExists(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be empty", nameof(directoryPath));
        }

        if (Directory.Exists(directoryPath))
        {
            _logger.LogDebug("Directory already exists: {Directory}", directoryPath);
            return false;
        }

        _logger.LogInformation("Creating directory: {Directory}", directoryPath);
        Directory.CreateDirectory(directoryPath);
        return true;
    }

    /// <summary>
    /// Creates a symlink to a media item.
    /// </summary>
    /// <param name="sourcePath">The source media file path.</param>
    /// <param name="targetDirectory">The target directory for the symlink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the created symlink.</returns>
    public Task<string> CreateSymlinkAsync(string sourcePath, string targetDirectory, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken; // Reserved for future use
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        // Ensure target directory exists (fallback behavior)
        EnsureDirectoryExists(targetDirectory);

        var fileName = Path.GetFileName(sourcePath);
        var symlinkPath = Path.Combine(targetDirectory, fileName);

        // If symlink already exists, remove it
        if (File.Exists(symlinkPath))
        {
            _logger.LogInformation("Removing existing symlink: {Path}", symlinkPath);
            File.Delete(symlinkPath);
        }

        _logger.LogInformation("Creating symlink: {Source} -> {Target}", sourcePath, symlinkPath);

        // Create symlink (Unix-specific, Windows requires different approach)
        try
        {
            File.CreateSymbolicLink(symlinkPath, sourcePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create symlink");
            throw;
        }

        return Task.FromResult(symlinkPath);
    }

    /// <summary>
    /// Removes a symlink.
    /// </summary>
    /// <param name="symlinkPath">The symlink path to remove.</param>
    public void RemoveSymlink(string symlinkPath)
    {
        if (!File.Exists(symlinkPath))
        {
            _logger.LogWarning("Symlink does not exist: {Path}", symlinkPath);
            return;
        }

        _logger.LogInformation("Removing symlink: {Path}", symlinkPath);
        File.Delete(symlinkPath);
    }

    /// <summary>
    /// Removes a directory if it exists.
    /// </summary>
    /// <param name="directoryPath">The directory path to remove.</param>
    /// <param name="force">If true, removes directory even if not empty. If false, only removes if empty.</param>
    /// <exception cref="InvalidOperationException">Thrown when directory is not empty and force is false.</exception>
    public void RemoveDirectory(string directoryPath, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be empty", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory does not exist: {Directory}", directoryPath);
            return;
        }

        // Check if directory is empty
        var files = Directory.GetFiles(directoryPath);
        var directories = Directory.GetDirectories(directoryPath);
        var hasContent = files.Length > 0 || directories.Length > 0;

        if (hasContent && !force)
        {
            throw new InvalidOperationException($"Directory is not empty: {directoryPath}. Use force=true to remove anyway.");
        }

        _logger.LogInformation("Removing directory: {Directory} (force={Force}, hasContent={HasContent})", directoryPath, force, hasContent);
        Directory.Delete(directoryPath, recursive: force);
    }

    /// <summary>
    /// Clears all symlinks in a directory.
    /// </summary>
    /// <param name="directory">The directory to clear.</param>
    public void ClearSymlinks(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Directory does not exist: {Directory}", directory);
            return;
        }

        _logger.LogInformation("Clearing symlinks in: {Directory}", directory);

        var files = Directory.GetFiles(directory);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                File.Delete(file);
                _logger.LogDebug("Removed symlink: {File}", file);
            }
        }
    }

    /// <summary>
    /// Lists all symlinks in a directory.
    /// </summary>
    /// <param name="directory">The directory to list symlinks from.</param>
    /// <returns>An array of symlink information including path and target.</returns>
    public SymlinkInfo[] ListSymlinks(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Directory does not exist: {Directory}", directory);
            return Array.Empty<SymlinkInfo>();
        }

        _logger.LogDebug("Listing symlinks in: {Directory}", directory);

        var symlinks = new System.Collections.Generic.List<SymlinkInfo>();
        var files = Directory.GetFiles(directory);

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                try
                {
                    var targetPath = fileInfo.LinkTarget ?? "unknown";
                    symlinks.Add(new SymlinkInfo
                    {
                        Path = file,
                        Target = targetPath,
                        Name = Path.GetFileName(file)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read symlink target for: {File}", file);
                }
            }
        }

        return symlinks.ToArray();
    }
}

/// <summary>
/// Information about a symlink.
/// </summary>
public class SymlinkInfo
{
    /// <summary>
    /// Gets or sets the full path to the symlink.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target path the symlink points to.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filename of the symlink.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
