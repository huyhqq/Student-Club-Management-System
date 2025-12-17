using ClubManagementApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ClubManagementApi.Controllers
{
    [Authorize]
    [Route("api/club-members")]
    [ApiController]
    public class ClubMembersController : ControllerBase
    {
        private readonly StudentClubContext _context;

        public ClubMembersController(StudentClubContext context)
        {
            _context = context;
        }

        private int CurrentUserId => GetUserIdFromHttpContext(HttpContext);
        private string CurrentUserFullName => HttpContext.User.FindFirst("FullName")?.Value ?? "Một thành viên";

        public static int GetUserIdFromHttpContext(HttpContext? context, ILogger? logger = null)
        {
            if (context == null)
            {
                logger?.LogError("HttpContext is null in JwtHelper.GetUserIdFromHttpContext.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }

            if (context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var userIdClaim = context.User.FindFirst("UserId")?.Value
                               ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
                {
                    logger?.LogInformation("Successfully retrieved UserId {UserId} from claims.", userId);
                    return userId;
                }
                logger?.LogError("Invalid UserId claim: {UserIdClaim}", userIdClaim);
                throw new UnauthorizedAccessException("ID người dùng không hợp lệ.");
            }

            var token = context.Request.Query["access_token"].ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                logger?.LogError("No access_token found in query parameters.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    logger?.LogError("UserId or nameidentifier claim not found in token.");
                    throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
                }

                if (!int.TryParse(userIdClaim, out var userId))
                {
                    logger?.LogError("Invalid UserId format in token: {UserIdClaim}", userIdClaim);
                    throw new UnauthorizedAccessException($"ID người dùng không hợp lệ: {userIdClaim}");
                }

                logger?.LogInformation("Successfully retrieved UserId {UserId} from access_token.", userId);
                return userId;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to parse JWT token from access_token.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }
        }


        public class JoinClubRequestDto
        {
            [Required(ErrorMessage = "ID CLB là bắt buộc")]
            [Range(1, int.MaxValue, ErrorMessage = "ID CLB không hợp lệ")]
            public int ClubId { get; set; }

            [Required(ErrorMessage = "Mã sinh viên là bắt buộc")]
            [StringLength(20, MinimumLength = 5, ErrorMessage = "Mã sinh viên phải từ 5 đến 20 ký tự")]
            public string StudentId { get; set; } = string.Empty;

            [Required(ErrorMessage = "Ngành học là bắt buộc")]
            [StringLength(200, MinimumLength = 2)]
            public string Major { get; set; } = string.Empty;

            [Required(ErrorMessage = "Năm học là bắt buộc")]
            [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "Năm học phải có định dạng YYYY-YYYY (ví dụ: 2023-2024)")]
            public string AcademicYear { get; set; } = string.Empty;

            [Required(ErrorMessage = "Giới thiệu bản thân là bắt buộc")]
            [MinLength(50, ErrorMessage = "Giới thiệu phải ít nhất 50 ký tự")]
            public string Introduction { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lý do tham gia là bắt buộc")]
            [MinLength(50, ErrorMessage = "Lý do phải ít nhất 50 ký tự")]
            public string Reason { get; set; } = string.Empty;

            [StringLength(200)]
            public string? ContactInfoOptional { get; set; }
        }

        public class ClubIdDto
        {
            [Required(ErrorMessage = "ID CLB là bắt buộc")]
            [Range(1, int.MaxValue)]
            public int ClubId { get; set; }
        }


        private IActionResult ValidationErrorResponse()
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new { Field = x.Key, Message = x.Value?.Errors.First().ErrorMessage })
                .ToList();

            return BadRequest(ApiResponse<object>.FailResponse("Dữ liệu không hợp lệ", errors));
        }

        // ==================== CÁC ENDPOINT ====================

        [HttpPost("join")]
        public async Task<IActionResult> JoinClub([FromBody] JoinClubRequestDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var clubId = dto.ClubId;

            var alreadyMember = await _context.ClubMembers
                .AnyAsync(m => m.ClubId == clubId && m.UserId == CurrentUserId && m.Status == "Approved");

            var alreadyRequested = await _context.ClubJoinRequests
                .AnyAsync(r => r.ClubId == clubId && r.UserId == CurrentUserId && r.Status == "Pending");

            if (alreadyMember)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn đã là thành viên của CLB này"));

            if (alreadyRequested)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn đã gửi yêu cầu tham gia, đang chờ duyệt"));

            var club = await _context.Clubs
                .Include(c => c.President)
                .FirstOrDefaultAsync(c => c.ClubId == clubId && c.Status == "Active");

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

            FireAndForget(async () =>
            {
                if (club.President != null)
                {
                    await NotificationService.SendAsync(
                        club.President.UserId,
                        "Yêu cầu tham gia CLB mới",
                        $"{CurrentUserFullName} (MSSV: {dto.StudentId}) muốn tham gia CLB {club.ClubName}"
                    );
                }
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Gửi yêu cầu tham gia thành công! Vui lòng chờ duyệt."));
        }

        [HttpPatch("{requestId}/approve")]
        [Authorize(Roles = "ClubLeader,Admin")]
        public async Task<IActionResult> ApproveMember(int requestId)
        {
            var request = await _context.ClubJoinRequests
                .Include(r => r.Club)
                .ThenInclude(c => c!.President)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && r.Status == "Pending");

            if (request == null)
                return NotFound(ApiResponse<object>.FailResponse("Yêu cầu không tồn tại hoặc đã được xử lý"));

            if (request.Club.PresidentId != CurrentUserId && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền duyệt yêu cầu này"));

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
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ApiResponse<object>.FailResponse("Duyệt thành viên thất bại"));
            }

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(
                    request.UserId,
                    "Chúc mừng! Bạn đã được duyệt vào CLB",
                    $"Bạn đã chính thức trở thành thành viên của CLB \"{request.Club.ClubName}\"!"
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Duyệt thành viên thành công"));
        }

        [HttpPatch("{memberId}/remove")]
        [Authorize(Roles = "ClubLeader,Admin")]
        public async Task<IActionResult> RemoveMember(int memberId)
        {
            var member = await _context.ClubMembers
                .Include(m => m.Club)
                .ThenInclude(c => c!.President)
                .FirstOrDefaultAsync(m => m.MemberId == memberId && m.Status == "Approved");

            if (member == null)
                return NotFound(ApiResponse<object>.FailResponse("Thành viên không tồn tại"));

            if (member.Club.PresidentId != CurrentUserId && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xóa thành viên này"));

            if (member.UserId == member.Club.PresidentId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể xóa chủ nhiệm CLB"));

            member.Status = "Removed";
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(
                    member.UserId,
                    "Bạn đã bị xóa khỏi CLB",
                    $"Bạn đã bị xóa khỏi CLB \"{member.Club.ClubName}\" bởi chủ nhiệm."
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đã xóa thành viên thành công"));
        }

        [HttpPatch("{requestId}/reject")]
        [Authorize(Roles = "ClubLeader,Admin")]
        public async Task<IActionResult> RejectRequest(int requestId)
        {
            var request = await _context.ClubJoinRequests
                .Include(r => r.Club)
                .ThenInclude(c => c!.President)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && r.Status == "Pending");

            if (request == null)
                return NotFound(ApiResponse<object>.FailResponse("Yêu cầu không tồn tại hoặc đã được xử lý"));

            if (request.Club.PresidentId != CurrentUserId && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền từ chối yêu cầu này"));

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(
                    request.UserId,
                    "Yêu cầu tham gia bị từ chối",
                    $"Rất tiếc, yêu cầu tham gia CLB \"{request.Club.ClubName}\" của bạn đã bị từ chối."
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đã từ chối yêu cầu tham gia"));
        }

        [HttpPost("leave")]
        public async Task<IActionResult> LeaveClub([FromBody] ClubIdDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var member = await _context.ClubMembers
                .Include(m => m.Club)
                .ThenInclude(c => c!.President)
                .FirstOrDefaultAsync(m => m.ClubId == dto.ClubId && m.UserId == CurrentUserId && m.Status == "Approved");

            if (member == null)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn không phải thành viên của CLB này"));

            if (member.Club.PresidentId == CurrentUserId)
                return BadRequest(ApiResponse<object>.FailResponse("Chủ nhiệm không thể rời CLB. Vui lòng chuyển giao trước."));

            member.Status = "Removed";
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(
                    member.Club.PresidentId!.Value,
                    "Thành viên đã rời CLB",
                    $"{CurrentUserFullName} đã rời khỏi CLB {member.Club.ClubName}"
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Bạn đã rời CLB thành công"));
        }

        [HttpPost("cancel-request")]
        public async Task<IActionResult> CancelRequest([FromBody] ClubIdDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var request = await _context.ClubJoinRequests
                .FirstOrDefaultAsync(r => r.ClubId == dto.ClubId && r.UserId == CurrentUserId && r.Status == "Pending");

            if (request == null)
                return BadRequest(ApiResponse<object>.FailResponse("Không tìm thấy yêu cầu đang chờ duyệt"));

            _context.ClubJoinRequests.Remove(request);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đã hủy yêu cầu tham gia"));
        }

        [HttpGet("my-clubs")]
        public async Task<IActionResult> MyClubs()
        {
            var clubs = await _context.ClubMembers
                .Where(m => m.UserId == CurrentUserId && m.Status == "Approved")
                .Include(m => m.Club)
                .ThenInclude(c => c!.President)
                .Select(m => new
                {
                    m.Club.ClubId,
                    m.Club.ClubName,
                    m.Club.Description,
                    PresidentName = m.Club.President!.FullName,
                    Role = m.Club.PresidentId == CurrentUserId ? "ClubLeader" : "Member"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(clubs));
        }

        [HttpGet("{clubId}/members")]
        public async Task<IActionResult> GetMembers(int clubId)
        {
            var club = await _context.Clubs
                .Include(c => c.President)
                .FirstOrDefaultAsync(c => c.ClubId == clubId);

            if (club == null) return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            var isLeader = club.PresidentId == CurrentUserId;
            var isMember = await _context.ClubMembers
                .AnyAsync(m => m.ClubId == clubId && m.UserId == CurrentUserId && m.Status == "Approved");

            if (!isLeader && !isMember && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xem danh sách thành viên"));

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

            return Ok(ApiResponse<object>.SuccessResponse(members));
        }

        [HttpGet("{clubId}/pending")]
        public async Task<IActionResult> GetPendingRequests(int clubId)
        {
            var club = await _context.Clubs.FirstOrDefaultAsync(c => c.ClubId == clubId);
            if (club == null) return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            if (club.PresidentId != CurrentUserId && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xem yêu cầu chờ duyệt"));

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

            return Ok(ApiResponse<object>.SuccessResponse(requests));
        }

        private static void FireAndForget(Func<Task> taskFunc)
        {
            Task.Run(async () =>
            {
                try
                {
                    await taskFunc();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FireAndForget error: {ex}");
                }
            });
        }
    }
}