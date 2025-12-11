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

        public record JoinClubRequestDto(
            int ClubId,
            string StudentId,
            string Major,
            string AcademicYear,
            string Introduction,
            string Reason,
            string? ContactInfoOptional
        );

        [HttpPost("join")]
        public async Task<IActionResult> JoinClub([FromBody] JoinClubRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse("Vui lòng nhập đầy đủ thông tin"));

            var clubId = dto.ClubId;

            var alreadyMember = await _context.ClubMembers
                .AnyAsync(m => m.ClubId == clubId && m.UserId == CurrentUserId && m.Status == "Approved");

            var alreadyRequested = await _context.ClubJoinRequests
                .AnyAsync(r => r.ClubId == clubId && r.UserId == CurrentUserId && r.Status == "Pending");

            if (alreadyMember)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn đã là thành viên của CLB này"));

            if (alreadyRequested)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn đã gửi yêu cầu tham gia, đang chờ duyệt"));

            var club = await _context.Clubs.FirstOrDefaultAsync(c => c.ClubId == clubId && c.Status == "Active");
            if (club == null)
                return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại hoặc chưa được kích hoạt"));

            var joinRequest = new ClubJoinRequest
            {
                ClubId = clubId,
                UserId = CurrentUserId,
                StudentId = dto.StudentId.Trim(),
                Major = dto.Major.Trim(),
                AcademicYear = dto.AcademicYear.Trim(),
                Introduction = dto.Introduction.Trim(),
                Reason = dto.Reason.Trim(),
                ContactInfoOptional = dto.ContactInfoOptional?.Trim(),
                Status = "Pending",
                CreatedAt = TimeZoneHelper.NowInVietnam
            };

            _context.ClubJoinRequests.Add(joinRequest);
            await _context.SaveChangesAsync();

            TaskHelper.FireAndForget(async () =>
            {
                var president = await _context.Users.FindAsync(club.PresidentId);
                var userName = User.FindFirst("FullName")?.Value ?? "Một thành viên";
                await NotificationService.SendAsync(
                    president!.UserId,
                    "Yêu cầu tham gia CLB mới",
                    $"{userName} muốn tham gia CLB {club.ClubName}"
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Gửi yêu cầu tham gia thành công! Vui lòng chờ duyệt."));
        }

        [HttpPatch("{requestId}/approve")]
        [Authorize(Roles = "ClubLeader")]
        public async Task<IActionResult> ApproveMember(int requestId)
        {
            var request = await _context.ClubJoinRequests
                .Include(r => r.Club)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && r.Status == "Pending");

            if (request == null)
                return NotFound(ApiResponse<object>.FailResponse("Yêu cầu không tồn tại hoặc đã được xử lý"));

            if (request.Club.PresidentId != CurrentUserId)
                return Forbid("Bạn không phải chủ nhiệm CLB này");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                request.Status = "Approved";
                request.ApprovedAt = TimeZoneHelper.NowInVietnam;

                var member = new ClubMember
                {
                    ClubId = request.ClubId,
                    UserId = request.UserId,
                    Status = "Approved",
                    JoinedDate = TimeZoneHelper.NowInVietnam
                };

                _context.ClubMembers.Add(member);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ApiResponse<object>.FailResponse("Duyệt thành viên thất bại"));
            }

            TaskHelper.FireAndForget(() =>
                NotificationService.SendAsync(
                    request.UserId,
                    "Chúc mừng! Bạn đã được duyệt vào CLB",
                    $"Bạn đã chính thức trở thành thành viên của CLB {request.Club.ClubName}!"
                ));

            return Ok(ApiResponse<String>.SuccessResponse("Duyệt thành viên thành công"));
        }


        [HttpPatch("{id}/remove")]
        [Authorize(Roles = "ClubLeader")]
        public async Task<IActionResult> RemoveMember(int id)
        {
            var member = await _context.ClubMembers
                .Include(r => r.Club)
                .FirstOrDefaultAsync(r => r.MemberId == id);

            if (member != null)
            {


                var memberRequest = await _context.ClubJoinRequests
                    .FirstOrDefaultAsync(m => m.UserId == member.UserId);

                if (memberRequest != null)
                {
                    _context.ClubJoinRequests.Remove(memberRequest);
                    await _context.SaveChangesAsync();
                    TaskHelper.FireAndForget(() =>
                  NotificationService.SendAsync(
                      member.UserId,
                      "Bạn đã bị xóa khỏi CLB",
                      $"Bạn đã bị xóa khỏi CLB {member.Club.ClubName} bởi chủ nhiệm."
                  ));
                }

                _context.ClubMembers.Remove(member);

                await _context.SaveChangesAsync();

                TaskHelper.FireAndForget(() =>
                    NotificationService.SendAsync(
                        member.UserId,
                        "Bạn đã bị xóa khỏi CLB",
                        $"Bạn đã bị xóa khỏi CLB {member.Club.ClubName} bởi chủ nhiệm."
                    ));

                return Ok(ApiResponse<String>.SuccessResponse("Đã xóa thành viên thành công"));
            }
            return Ok(ApiResponse<string>.SuccessResponse("Đã xóa thành viên thành công"));

        }

        [HttpPatch("{id}/reject")]
        [Authorize(Roles = "ClubLeader")]
        public async Task<IActionResult> RejectOrRemove(int id)
        {
            var pendingRequest = await _context.ClubJoinRequests
                .Include(r => r.Club)
                .FirstOrDefaultAsync(r => r.RequestId == id && r.Status == "Pending");

            if (pendingRequest != null)
            {
                if (pendingRequest.Club.PresidentId != CurrentUserId)
                    return Forbid("Bạn không có quyền");

                pendingRequest.Status = "Rejected";
                pendingRequest.ApprovedAt = TimeZoneHelper.NowInVietnam;

                await _context.SaveChangesAsync();

                TaskHelper.FireAndForget(() =>
                    NotificationService.SendAsync(
                        pendingRequest.UserId,
                        "Yêu cầu tham gia bị từ chối",
                        $"Rất tiếc, yêu cầu tham gia CLB {pendingRequest.Club.ClubName} đã bị từ chối."
                    ));

                return Ok(ApiResponse<String>.SuccessResponse("Đã từ chối yêu cầu tham gia"));
            }

            var member = await _context.ClubMembers
                .Include(m => m.Club)
                .FirstOrDefaultAsync(m => m.MemberId == id && m.Status == "Approved");

            if (member == null)
                return NotFound(ApiResponse<object>.FailResponse("Thành viên không tồn tại"));

            if (member.Club.PresidentId != CurrentUserId)
                return Forbid("Bạn không có quyền");

            if (member.UserId == member.Club.PresidentId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể xóa chủ nhiệm CLB"));

            member.Status = "Removed";
            await _context.SaveChangesAsync();

            TaskHelper.FireAndForget(() =>
                NotificationService.SendAsync(
                    member.UserId,
                    "Bạn đã bị xóa khỏi CLB",
                    $"Bạn đã bị xóa khỏi CLB {member.Club.ClubName} bởi chủ nhiệm."
                ));

            return Ok(ApiResponse<String>.SuccessResponse("Đã xóa thành viên thành công"));
        }

        [HttpPost("leave")]
        public async Task<IActionResult> LeaveClub([FromBody] int clubId)
        {
            var member = await _context.ClubMembers
                .Include(m => m.Club)
                .FirstOrDefaultAsync(m => m.ClubId == clubId && m.UserId == CurrentUserId && m.Status == "Approved");

            if (member == null)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn không phải thành viên của CLB này"));

            if (member.Club.PresidentId == CurrentUserId)
                return BadRequest(ApiResponse<object>.FailResponse("Chủ nhiệm không thể rời CLB. Vui lòng chuyển giao trước."));

            member.Status = "Removed";
            await _context.SaveChangesAsync();

            TaskHelper.FireAndForget(() =>
                NotificationService.SendAsync(
                    member.Club.PresidentId.Value,
                    "Thành viên đã rời CLB",
                    $"{User.FindFirst("FullName")?.Value ?? "Một thành viên"} đã rời khỏi CLB {member.Club.ClubName}"
                ));

            return Ok(ApiResponse<String>.SuccessResponse("Bạn đã rời CLB thành công"));
        }

        [HttpPost("cancel-request")]
        public async Task<IActionResult> CancelRequest([FromBody] int clubId)
        {
            var request = await _context.ClubJoinRequests
                .FirstOrDefaultAsync(r => r.ClubId == clubId && r.UserId == CurrentUserId && r.Status == "Pending");

            if (request == null)
                return BadRequest(ApiResponse<object>.FailResponse("Không tìm thấy yêu cầu"));

            _context.ClubJoinRequests.Remove(request);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<String>.SuccessResponse("Đã hủy yêu cầu tham gia"));
        }

        [HttpGet("my-clubs")]
        public async Task<IActionResult> MyClubs()
        {
            var clubs = await _context.ClubMembers
                .Where(m => m.UserId == CurrentUserId && m.Status == "Approved")
                .Include(m => m.Club)
                .ThenInclude(c => c.President)
                .Select(m => new
                {
                    m.Club.ClubId,
                    m.Club.ClubName,
                    m.Club.Description,
                    PresidentName = m.Club.President!.FullName,
                    Role = m.Club.PresidentId == CurrentUserId ? "ClubLeader" : "Member"
                })
                .ToListAsync();

            return Ok(ApiResponse<Object>.SuccessResponse(clubs));
        }

        [HttpGet("{clubId}/members")]
        public async Task<IActionResult> GetMembers(int clubId)
        {
            var club = await _context.Clubs.Include(c => c.President).FirstOrDefaultAsync(c => c.ClubId == clubId);
            if (club == null) return NotFound();

            var isLeader = club.PresidentId == CurrentUserId;
            var isMember = await _context.ClubMembers.AnyAsync(m => m.ClubId == clubId && m.UserId == CurrentUserId && m.Status == "Approved");
            if (!isLeader && !isMember && !User.IsInRole("Admin"))
                return Forbid();

            var members = await _context.ClubMembers
                .Where(m => m.ClubId == clubId && m.Status == "Approved")
                .Include(m => m.User)
                .Select(m => new
                {
                    MemberId = m.MemberId,
                    m.UserId,
                    FullName = m.User.FullName,
                    Email = m.User.Email,
                    JoinedDate = m.JoinedDate,
                    Role = m.UserId == club.PresidentId ? "ClubLeader" : "Member"
                })
                .ToListAsync();

            return Ok(ApiResponse<Object>.SuccessResponse(members));
        }

        [HttpGet("{clubId}/pending")]
        public async Task<IActionResult> GetPendingRequests(int clubId)
        {
            var club = await _context.Clubs.FirstOrDefaultAsync(c => c.ClubId == clubId);
            if (club == null) return NotFound();
            if (club.PresidentId != CurrentUserId) return Forbid();

            var requests = await _context.ClubJoinRequests
                .Where(r => r.ClubId == clubId && r.Status == "Pending")
                .Include(r => r.User)
                .Select(r => new
                {
                    RequestId = r.RequestId,
                    r.UserId,
                    FullName = r.User.FullName,
                    Email = r.User.Email,
                    r.StudentId,
                    r.Major,
                    r.AcademicYear,
                    r.Introduction,
                    r.Reason,
                    r.ContactInfoOptional,
                    RequestedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<Object>.SuccessResponse(requests));
        }
    }
}