using Shelfie.Shared;

namespace Shelfie.Client;

public sealed record PlannedNotification(int Id, DateTime NotifyAt, string Title, string Body);

/// <summary>
/// Decides which local notifications to schedule: one digest per day on which something reaches
/// "expires in N days". Pure logic — the app passes the plan to the platform notification API.
/// </summary>
public static class NotificationPlanner
{
    public const int MaxNotifications = 30;

    /// <param name="items">Current inventory.</param>
    /// <param name="now">Local time.</param>
    /// <param name="daysBefore">How many days before expiry to warn.</param>
    /// <param name="at">Time of day for the digest.</param>
    /// <param name="alreadyNotifiedToday">Whether today's digest was already shown (avoid repeating it on every reschedule).</param>
    public static List<PlannedNotification> Plan(IEnumerable<ItemDto> items, DateTime now, int daysBefore, TimeOnly at, bool alreadyNotifiedToday)
    {
        var today = DateOnly.FromDateTime(now);
        var result = new List<PlannedNotification>();

        var byDay = items
            .Where(i => !i.IsDeleted && i.Quantity > 0 && i.ExpirationDate is not null)
            .Select(i => (Item: i, Day: Max(i.ExpirationDate!.Value.AddDays(-daysBefore), today)))
            .Where(x => x.Item.ExpirationDate!.Value >= today)  // already expired: shown on the dashboard instead
            .GroupBy(x => x.Day)
            .OrderBy(g => g.Key);

        foreach (var group in byDay)
        {
            var when = group.Key.ToDateTime(at);
            if (group.Key == today && when <= now)
            {
                if (alreadyNotifiedToday)
                    continue;
                when = now.AddMinutes(1); // the digest time has passed today: show it shortly
            }

            var names = group.Select(x => x.Item).OrderBy(i => i.ExpirationDate).Select(i => i.Name).ToList();
            var body = string.Join(", ", names.Take(3)) + (names.Count > 3 ? $" и ещё {names.Count - 3}" : string.Empty);
            var title = names.Count == 1 ? "Скоро истекает срок годности" : $"Скоро истекает срок: {names.Count} шт.";

            result.Add(new PlannedNotification(group.Key.DayNumber, when, title, body));
            if (result.Count == MaxNotifications)
                break;
        }

        return result;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
