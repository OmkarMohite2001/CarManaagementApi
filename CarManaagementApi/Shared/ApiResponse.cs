using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Shared;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; } = true;
    public string Message { get; init; } = "OK";
    public T? Data { get; init; }
    public ApiMeta? Meta { get; init; }
}

public sealed class ApiMeta
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
}

public sealed class ApiErrorResponse
{
    public bool Success { get; init; } = false;
    public string Message { get; init; } = "Error";
    public IReadOnlyCollection<ApiErrorItem> Errors { get; init; } = Array.Empty<ApiErrorItem>();
    public string TraceId { get; init; } = string.Empty;
}

public sealed class ApiErrorItem
{
    public string Field { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult OkResponse<T>(T data, string message = "OK", ApiMeta? meta = null)
    {
        return Ok(new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Meta = meta
        });
    }

    protected IActionResult CreatedResponse<T>(string actionName, object routeValues, T data, string message = "Created")
    {
        return CreatedAtAction(actionName, routeValues, new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        });
    }

    protected ActionResult<ApiResponse<object>> NoContentResponse()
    {
        return new ObjectResult(new ApiResponse<object>
        {
            Success = true,
            Message = "No Content",
            Data = null
        })
        {
            StatusCode = StatusCodes.Status204NoContent
        };
    }

    protected IActionResult ErrorResponse(
        int statusCode,
        string message,
        IEnumerable<ApiErrorItem>? errors = null)
    {
        var payload = new ApiErrorResponse
        {
            Message = message,
            Errors = errors?.ToArray() ?? Array.Empty<ApiErrorItem>(),
            TraceId = HttpContext.TraceIdentifier
        };

        return StatusCode(statusCode, payload);
    }
}
