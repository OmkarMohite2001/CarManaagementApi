using CarManaagementApi.Persistence;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public NotificationsController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.GetUserId();
        var unreadCount = await _db.Notifications
            .CountAsync(x => !x.IsRead && (x.UserId == null || x.UserId == userId));

        return OkResponse(new { unreadCount });
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();

        var rows = await _db.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == null || x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (object)new
            {
                id = x.NotificationId,
                title = x.Title,
                message = x.Message,
                isRead = x.IsRead,
                createdAt = x.CreatedAt
            })
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkRead(string id)
    {
        var userId = User.GetUserId();
        var notification = await _db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && (x.UserId == null || x.UserId == userId));
        if (notification is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Notification not found");
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return OkResponse(new { id = notification.NotificationId, isRead = true }, "Notification marked as read");
    }

    [HttpPatch("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.GetUserId();

        var notifications = await _db.Notifications
            .Where(x => !x.IsRead && (x.UserId == null || x.UserId == userId))
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return OkResponse(new { updated = notifications.Count }, "All notifications marked as read");
    }
}
