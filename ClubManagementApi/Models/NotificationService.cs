
namespace ClubManagementApi.Models
{
    public static class NotificationService
    {
        private static StudentClubContext _context = null!;

        public static void Initialize(StudentClubContext context)
        {
            _context = context;
        }

        public static async Task SendAsync(int userId, string title, string message)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = TimeZoneHelper.NowInVietnam
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }


        public static async Task SendToManyAsync(IEnumerable<int> userIds, string title, string message)
        {
            var notifications = userIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = TimeZoneHelper.NowInVietnam
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }
}