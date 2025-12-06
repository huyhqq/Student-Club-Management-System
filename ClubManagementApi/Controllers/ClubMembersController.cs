using ClubManagementApi.DTO;
using ClubManagementApi.Models;
using ClubManagementApi.Helpers;
using ClubManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClubManagementApi.Data;

namespace ClubManagementApi.Controllers
{
    [Authorize]
    [Route("api/club-members")]
    public class ClubMembersController : ControllerBase
    {
        private readonly StudentClubContext _context;
        public ClubMembersController(StudentClubContext context) => _context = context;

        private int CurrentUserId => JwtHelper.GetUserIdFromHttpContext(HttpContext);

        [HttpPost("join")]
        public async Task<IActionResult> JoinClub(int clubId)
        {
            if (_context.ClubMembers.Any(m => m.ClubId == clubId && m.UserId == CurrentUserId))
                return BadRequest(ApiResponse<object>.FailResponse("Bạn đã gửi yêu cầu hoặc là thành viên"));

            var club = await _context.Clubs.FindAsync(clubId);
            if (club == null || club.Status != "Active")
                return BadRequest(ApiResponse<object>.FailResponse("CLB không tồn tại hoặc chưa được duyệt"));

            var member = new ClubMember
            {
                ClubId = clubId,
                UserId = CurrentUserId,
                Status = "Pending",
                JoinedDate = TimeZoneHelper.NowInVietnam
            };

            _context.ClubMembers.Add(member);
            await _context.SaveChangesAsync();

            TaskHelper.FireAndForget(async () =>
            {
                var president = await _context.Users.FindAsync(club.PresidentId);
                await NotificationService.SendAsync(
                    president!.UserId,
                    "Yêu cầu tham gia CLB",
                    $"Thành viên {User.FindFirst("FullName")?.Value} muốn tham gia {club.ClubName}"
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đã gửi yêu cầu tham gia CLB"));
        }

        [HttpPatch("{memberId}/approve")]
        [Authorize(Roles = "ClubLeader")]
        public async Task<IActionResult> ApproveMember(int memberId)
        {
            var member = await _context.ClubMembers
                .Include(m => m.Club)
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.MemberId == memberId);

            if (member == null) return NotFound();
            if (member.Club.PresidentId != CurrentUserId) return StatusCode(403,
                ApiResponse<object>.FailResponse("Bạn không có quyền duyệt"));

            member.Status = "Approved";
            await _context.SaveChangesAsync();

            TaskHelper.FireAndForget(async () =>
                await NotificationService.SendAsync(
                    member.UserId,
                    "Đã được duyệt vào CLB",
                    $"Bạn đã được chấp nhận tham gia {member.Club.ClubName}!"
                ));

            return Ok(ApiResponse<string>.SuccessResponse(null, "Duyệt thành công"));
        }

        [HttpPatch("{memberId}/remove")]
        [Authorize(Roles = "ClubLeader")]
        public async Task<IActionResult> RemoveMember(int memberId)
        {
            var member = await _context.ClubMembers
                .Include(m => m.Club)
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.MemberId == memberId);

            if (member == null) return NotFound();
            if (member.UserId == member.Club.PresidentId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể xóa Chủ nhiệm"));

            if (member.Club.PresidentId != CurrentUserId)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xóa"));

            member.Status = "Removed";
            await _context.SaveChangesAsync();

            TaskHelper.FireAndForget(async () =>
                await NotificationService.SendAsync(
                    member.UserId,
                    "Bị xóa khỏi CLB",
                    $"Bạn đã bị xóa khỏi CLB {member.Club.ClubName}"
                ));

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đã xóa thành viên"));
        }

        [HttpGet("my-clubs")]
        public async Task<IActionResult> MyClubs()
        {
            var clubs = await _context.ClubMembers
                .Where(m => m.UserId == CurrentUserId && m.Status == "Approved")
                .Include(m => m.Club)
                .Select(m => new
                {
                    m.Club.ClubId,
                    m.Club.ClubName,
                    Role = m.Club.PresidentId == CurrentUserId ? "ClubLeader" : "Member"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(clubs));
        }

        [HttpGet("{clubId}/members")]
        public async Task<IActionResult> GetMembers(int clubId)
        {
            var club = await _context.Clubs.FindAsync(clubId);
            if (club == null)
                return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            var members = await _context.ClubMembers
                .Where(m => m.ClubId == clubId && m.Status == "Approved")
                .Include(m => m.User)
                .Select(m => new
                {
                    m.MemberId,
                    m.UserId,
                    FullName = m.User.FullName,
                    Email = m.User.Email,
                    JoinedDate = m.JoinedDate,
                    Role = m.UserId == club.PresidentId ? "ClubLeader" : "Member"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(members));
        }
    }
}
