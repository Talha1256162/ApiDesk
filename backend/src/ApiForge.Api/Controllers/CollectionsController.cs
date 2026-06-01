using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Collections;
using ApiForge.Shared.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class CollectionsController(ICollectionService collectionService) : ApiControllerBase
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
    public async Task<IActionResult> GetCollectionRequests(Guid collectionId, CancellationToken cancellationToken)
    {
        return FromResult(await collectionService.GetCollectionRequestsAsync(collectionId, cancellationToken));
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
}
