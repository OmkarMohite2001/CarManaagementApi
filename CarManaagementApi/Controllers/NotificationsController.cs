using CarManaagementApi.Services;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public NotificationsController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("unread-count")]
    public IActionResult GetUnreadCount()
    {
        lock (_store.SyncRoot)
        {
            var unreadCount = _store.Notifications.Count(x => !x.IsRead);
            return OkResponse(new { unreadCount });
        }
    }

    [HttpGet]
    public IActionResult GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        lock (_store.SyncRoot)
        {
            var query = _store.Notifications
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (object)new
                {
                    id = x.Id,
                    title = x.Title,
                    message = x.Message,
                    isRead = x.IsRead,
                    createdAt = x.CreatedAt
                });

            var (items, meta) = query.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPatch("{id}/read")]
    public IActionResult MarkRead(string id)
    {
        lock (_store.SyncRoot)
        {
            var notification = _store.Notifications.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (notification is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Notification not found");
            }

            notification.IsRead = true;
            return OkResponse(new { id = notification.Id, isRead = true }, "Notification marked as read");
        }
    }

    [HttpPatch("mark-all-read")]
    public IActionResult MarkAllRead()
    {
        int count;
        lock (_store.SyncRoot)
        {
            count = 0;
            foreach (var notification in _store.Notifications.Where(x => !x.IsRead))
            {
                notification.IsRead = true;
                count++;
            }
        }

        return OkResponse(new { updated = count }, "All notifications marked as read");
    }
}
