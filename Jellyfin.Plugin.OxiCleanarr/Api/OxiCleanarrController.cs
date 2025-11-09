using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OxiCleanarr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OxiCleanarr.Api;

/// <summary>
/// API Controller for OxiCleanarr Bridge plugin.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("api/oxicleanarr")]
[Produces(MediaTypeNames.Application.Json)]
public class OxiCleanarrController : ControllerBase
{
    private readonly ILogger<OxiCleanarrController> _logger;
    private readonly SymlinkManager _symlinkManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OxiCleanarrController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="symlinkManager">The symlink manager.</param>
    public OxiCleanarrController(
        ILogger<OxiCleanarrController> logger,
        SymlinkManager symlinkManager)
    {
        _logger = logger;
        _symlinkManager = symlinkManager;
    }

    /// <summary>
    /// Creates symlinks for media items.
    /// </summary>
    /// <param name="request">The request containing items to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success response.</returns>
    [HttpPost("symlinks/add")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AddItemsResponse>> AddSymlinks(
        [FromBody] AddItemsRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { error = "No items provided" });
        }

        _logger.LogInformation("Received request to create {Count} symlinks", request.Items.Count);

        var createdSymlinks = new List<string>();
        var errors = new List<string>();

        foreach (var item in request.Items)
        {
            try
            {
                var symlinkPath = await _symlinkManager.CreateSymlinkAsync(
                    item.SourcePath,
                    item.TargetDirectory,
                    cancellationToken);

                createdSymlinks.Add(symlinkPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create symlink for {Path}", item.SourcePath);
                errors.Add($"{item.SourcePath}: {ex.Message}");
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
    /// Removes symlinks for media items.
    /// </summary>
    /// <param name="request">The request containing symlink paths to remove.</param>
    /// <returns>Success response.</returns>
    [HttpPost("symlinks/remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<RemoveItemsResponse> RemoveSymlinks([FromBody] RemoveItemsRequest request)
    {
        if (request?.SymlinkPaths == null || request.SymlinkPaths.Count == 0)
        {
            return BadRequest(new { error = "No symlink paths provided" });
        }

        _logger.LogInformation("Received request to remove {Count} symlinks", request.SymlinkPaths.Count);

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
    /// Lists all symlinks in a directory.
    /// </summary>
    /// <param name="directory">The directory to list symlinks from.</param>
    /// <returns>List of symlinks.</returns>
    [HttpGet("symlinks/list")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<ListSymlinksResponse> ListSymlinks([FromQuery] string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return BadRequest(new { error = "Directory parameter is required" });
        }

        _logger.LogInformation("Received request to list symlinks in {Directory}", directory);

        try
        {
            var symlinks = _symlinkManager.ListSymlinks(directory);
            var symlinkNames = symlinks.Select(s => s.Name).ToArray();
            string message = symlinks.Length == 0 
                ? "No symlinks found in directory" 
                : $"Found {symlinks.Length} symlink(s)";

            return Ok(new ListSymlinksResponse
            {
                Symlinks = symlinks,
                Count = symlinks.Length,
                SymlinkNames = symlinkNames,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list symlinks");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Creates a directory.
    /// </summary>
    /// <param name="request">The request containing the directory path.</param>
    /// <returns>Success response.</returns>
    [HttpPost("directories/create")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<CreateDirectoryResponse> CreateDirectory([FromBody] CreateDirectoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Directory))
        {
            return BadRequest(new { error = "Directory path is required" });
        }

        _logger.LogInformation("Received request to create directory: {Directory}", request.Directory);

        try
        {
            var created = _symlinkManager.EnsureDirectoryExists(request.Directory);
            return Ok(new CreateDirectoryResponse
            {
                Success = true,
                Directory = request.Directory,
                Created = created,
                Message = created ? "Directory created successfully" : "Directory already exists"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create directory: {Directory}", request.Directory);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a directory.
    /// </summary>
    /// <param name="request">The request containing the directory path and force flag.</param>
    /// <returns>Success response.</returns>
    [HttpDelete("directories/remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<RemoveDirectoryResponse> RemoveDirectory([FromBody] RemoveDirectoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Directory))
        {
            return BadRequest(new { error = "Directory path is required" });
        }

        _logger.LogInformation("Received request to remove directory: {Directory} (force={Force})", request.Directory, request.Force);

        try
        {
            _symlinkManager.RemoveDirectory(request.Directory, request.Force);
            return Ok(new RemoveDirectoryResponse
            {
                Success = true,
                Directory = request.Directory,
                Message = "Directory removed successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot remove non-empty directory: {Directory}", request.Directory);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove directory: {Directory}", request.Directory);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets plugin status and version.
    /// </summary>
    /// <returns>Plugin status.</returns>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatusResponse> GetStatus()
    {
        return Ok(new StatusResponse
        {
            Version = Plugin.Instance?.Version.ToString() ?? "unknown"
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
    /// Gets or sets the target directory where the symlink should be created.
    /// </summary>
    [Required]
    public string TargetDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deletion date (optional, for informational purposes only).
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
}

/// <summary>
/// Response model for list symlinks endpoint.
/// </summary>
public class ListSymlinksResponse
{
    /// <summary>
    /// Gets or sets the array of symlinks.
    /// </summary>
    public SymlinkInfo[] Symlinks { get; set; } = Array.Empty<SymlinkInfo>();

    /// <summary>
    /// Gets or sets the count of symlinks.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the array of symlink names for easy parsing.
    /// </summary>
    public string[] SymlinkNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets a message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request model for creating a directory.
/// </summary>
public class CreateDirectoryRequest
{
    /// <summary>
    /// Gets or sets the directory path to create.
    /// </summary>
    [Required]
    public string Directory { get; set; } = string.Empty;
}

/// <summary>
/// Response model for creating a directory.
/// </summary>
public class CreateDirectoryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the directory path that was created.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the directory was newly created.
    /// </summary>
    public bool Created { get; set; }

    /// <summary>
    /// Gets or sets a message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request model for removing a directory.
/// </summary>
public class RemoveDirectoryRequest
{
    /// <summary>
    /// Gets or sets the directory path to remove.
    /// </summary>
    [Required]
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to force removal even if directory is not empty.
    /// </summary>
    public bool Force { get; set; }
}

/// <summary>
/// Response model for removing a directory.
/// </summary>
public class RemoveDirectoryResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the directory path that was removed.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

#endregion
