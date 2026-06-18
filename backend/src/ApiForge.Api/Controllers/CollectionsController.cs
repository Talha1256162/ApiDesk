using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Collections;
using ApiForge.Api.SignalR;
using ApiForge.Shared.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class CollectionsController(ICollectionService collectionService, IHubContext<CollaborationHub> hubContext) : ApiControllerBase
{
    [HttpGet("workspaces/{workspaceId:guid}/collections")]
    public async Task<IActionResult> GetCollections(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.GetCollectionsAsync(workspaceId, request, cancellationToken));
    }

    [HttpPost("collections")]
    public async Task<IActionResult> CreateCollection(CreateCollectionRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.CreateCollectionAsync(request, cancellationToken));
    }

    [HttpGet("collections/{collectionId:guid}/requests")]
    public async Task<IActionResult> GetCollectionRequests(Guid collectionId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.GetCollectionRequestsAsync(collectionId, request, cancellationToken));
    }

    [HttpPut("collections/{collectionId:guid}")]
    public async Task<IActionResult> UpdateCollection(Guid collectionId, UpdateCollectionRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.UpdateCollectionAsync(collectionId, request, cancellationToken));
    }

    [HttpDelete("collections/{collectionId:guid}")]
    public async Task<IActionResult> DeleteCollection(Guid collectionId, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.DeleteCollectionAsync(collectionId, cancellationToken));
    }

    [HttpPost("collections/{collectionId:guid}/folders")]
    public async Task<IActionResult> CreateFolder(Guid collectionId, CreateFolderRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.CreateFolderAsync(collectionId, request, cancellationToken));
    }

    [HttpPost("folders/{folderId:guid}/requests")]
    public async Task<IActionResult> CreateRequest(Guid folderId, SaveApiRequestRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.CreateRequestAsync(folderId, request, cancellationToken));
    }

    [HttpPost("collections/{collectionId:guid}/requests")]
    public async Task<IActionResult> CreateRequestInCollection(Guid collectionId, SaveApiRequestRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.CreateRequestInCollectionAsync(collectionId, request, cancellationToken));
    }

    [HttpGet("requests/{requestId:guid}")]
    public async Task<IActionResult> GetRequest(Guid requestId, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.GetRequestAsync(requestId, cancellationToken));
    }

    [HttpPut("requests/{requestId:guid}")]
    public async Task<IActionResult> UpdateRequest(Guid requestId, SaveApiRequestRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.UpdateRequestAsync(requestId, request, cancellationToken));
    }

    [HttpDelete("requests/{requestId:guid}")]
    public async Task<IActionResult> DeleteRequest(Guid requestId, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.DeleteRequestAsync(requestId, cancellationToken));
    }

    [HttpPost("requests/{requestId:guid}/examples")]
    public async Task<IActionResult> SaveResponseExample(Guid requestId, SaveResponseExampleRequest request, CancellationToken cancellationToken)
    {
        var result = await collectionService.SaveResponseExampleAsync(requestId, request, cancellationToken);
        if (result.Succeeded && result.Data is not null)
        {
            await hubContext.Clients.All.SendAsync("responseExampleSaved", result.Data, cancellationToken);
        }
        return FromResult(result);
    }

    [HttpGet("collections/{collectionId:guid}/export")]
    public async Task<IActionResult> ExportCollection(Guid collectionId, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.ExportCollectionAsync(collectionId, cancellationToken));
    }

    [HttpPost("workspaces/{workspaceId:guid}/collections/import")]
    public async Task<IActionResult> ImportCollection(Guid workspaceId, ImportCollectionRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.ImportCollectionAsync(workspaceId, request, cancellationToken));
    }

    [HttpGet("workspaces/{workspaceId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid workspaceId, [FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.GetCommentsAsync(workspaceId, entityType, entityId, cancellationToken));
    }

    [HttpPost("workspaces/{workspaceId:guid}/comments")]
    public async Task<IActionResult> CreateComment(Guid workspaceId, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var result = await collectionService.CreateCommentAsync(workspaceId, request, cancellationToken);
        if (result.Succeeded && result.Data is not null)
        {
            await hubContext.Clients.Group(CollaborationHub.WorkspaceGroup(workspaceId)).SendAsync("commentCreated", result.Data, cancellationToken);
        }
        return FromResult(result);
    }
}
