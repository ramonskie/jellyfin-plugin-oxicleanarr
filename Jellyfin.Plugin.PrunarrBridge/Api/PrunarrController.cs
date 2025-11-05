using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PrunarrBridge.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PrunarrBridge.Api;

/// <summary>
/// API Controller for Prunarr Bridge plugin.
/// </summary>
[ApiController]
[Route("api/prunarr")]
[Produces(MediaTypeNames.Application.Json)]
public class PrunarrController : ControllerBase
{
    private readonly ILogger<PrunarrController> _logger;
    private readonly SymlinkManager _symlinkManager;
    private readonly VirtualFolderManager _virtualFolderManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrunarrController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public PrunarrController(
        ILogger<PrunarrController> _logger,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory)
    {
        this._logger = logger;
        _symlinkManager = new SymlinkManager(loggerFactory.CreateLogger<SymlinkManager>(), libraryManager);
        _virtualFolderManager = new VirtualFolderManager(loggerFactory.CreateLogger<VirtualFolderManager>(), libraryManager);
    }

    /// <summary>
    /// Adds items to the "Leaving Soon" library.
    /// </summary>
    /// <param name="request">The request containing items to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success response.</returns>
    [HttpPost("leaving-soon/add")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AddItemsResponse>> AddLeavingSoonItems(
        [FromBody] AddItemsRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { error = "No items provided" });
        }

        _logger.LogInformation("Received request to add {Count} items to Leaving Soon", request.Items.Count);

        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return StatusCode(500, new { error = "Plugin configuration not available" });
        }

        var createdSymlinks = new List<string>();
        var errors = new List<string>();

        foreach (var item in request.Items)
        {
            try
            {
                var symlinkPath = await _symlinkManager.CreateSymlinkAsync(
                    item.SourcePath,
                    config.SymlinkBasePath,
                    cancellationToken);
                
                createdSymlinks.Add(symlinkPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create symlink for {Path}", item.SourcePath);
                errors.Add($"{item.SourcePath}: {ex.Message}");
            }
        }

        // Trigger library scan if configured
        if (config.AutoCreateVirtualFolder && createdSymlinks.Count > 0)
        {
            try
            {
                await _virtualFolderManager.ScanLibraryAsync(config.VirtualFolderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan library");
                errors.Add($"Library scan failed: {ex.Message}");
            }
        }

        return Ok(new AddItemsResponse
        {
            Success = true,
            CreatedSymlinks = createdSymlinks,
            Errors = errors
        });
    }

    /// <summary>
    /// Removes items from the "Leaving Soon" library.
    /// </summary>
    /// <param name="request">The request containing items to remove.</param>
    /// <returns>Success response.</returns>
    [HttpPost("leaving-soon/remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<RemoveItemsResponse> RemoveLeavingSoonItems([FromBody] RemoveItemsRequest request)
    {
        if (request?.SymlinkPaths == null || request.SymlinkPaths.Count == 0)
        {
            return BadRequest(new { error = "No symlink paths provided" });
        }

        _logger.LogInformation("Received request to remove {Count} items from Leaving Soon", request.SymlinkPaths.Count);

        var removed = new List<string>();
        var errors = new List<string>();

        foreach (var path in request.SymlinkPaths)
        {
            try
            {
                _symlinkManager.RemoveSymlink(path);
                removed.Add(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove symlink {Path}", path);
                errors.Add($"{path}: {ex.Message}");
            }
        }

        return Ok(new RemoveItemsResponse
        {
            Success = true,
            RemovedSymlinks = removed,
            Errors = errors
        });
    }

    /// <summary>
    /// Clears all items from the "Leaving Soon" library.
    /// </summary>
    /// <returns>Success response.</returns>
    [HttpPost("leaving-soon/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult ClearLeavingSoon()
    {
        _logger.LogInformation("Received request to clear Leaving Soon library");

        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return StatusCode(500, new { error = "Plugin configuration not available" });
        }

        try
        {
            _symlinkManager.ClearSymlinks(config.SymlinkBasePath);
            return Ok(new { success = true, message = "Leaving Soon library cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Leaving Soon library");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets plugin status and configuration.
    /// </summary>
    /// <returns>Plugin status.</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatusResponse> GetStatus()
    {
        var config = Plugin.Instance?.Configuration;
        
        return Ok(new StatusResponse
        {
            Version = Plugin.Instance?.Version.ToString() ?? "unknown",
            SymlinkBasePath = config?.SymlinkBasePath ?? "not configured",
            VirtualFolderName = config?.VirtualFolderName ?? "not configured",
            AutoCreateVirtualFolder = config?.AutoCreateVirtualFolder ?? false
        });
    }
}

#region Request/Response Models

/// <summary>
/// Request model for adding items.
/// </summary>
public class AddItemsRequest
{
    /// <summary>
    /// Gets or sets the items to add.
    /// </summary>
    [Required]
    public List<MediaItem> Items { get; set; } = new();
}

/// <summary>
/// Media item model.
/// </summary>
public class MediaItem
{
    /// <summary>
    /// Gets or sets the source path of the media file.
    /// </summary>
    [Required]
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deletion date (optional).
    /// </summary>
    public DateTime? DeletionDate { get; set; }
}

/// <summary>
/// Response model for adding items.
/// </summary>
public class AddItemsResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the list of created symlink paths.
    /// </summary>
    public List<string> CreatedSymlinks { get; set; } = new();

    /// <summary>
    /// Gets or sets any errors that occurred.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Request model for removing items.
/// </summary>
public class RemoveItemsRequest
{
    /// <summary>
    /// Gets or sets the symlink paths to remove.
    /// </summary>
    [Required]
    public List<string> SymlinkPaths { get; set; } = new();
}

/// <summary>
/// Response model for removing items.
/// </summary>
public class RemoveItemsResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the list of removed symlink paths.
    /// </summary>
    public List<string> RemovedSymlinks { get; set; } = new();

    /// <summary>
    /// Gets or sets any errors that occurred.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Response model for status endpoint.
/// </summary>
public class StatusResponse
{
    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the symlink base path.
    /// </summary>
    public string SymlinkBasePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the virtual folder name.
    /// </summary>
    public string VirtualFolderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether auto-create virtual folder is enabled.
    /// </summary>
    public bool AutoCreateVirtualFolder { get; set; }
}

#endregion
