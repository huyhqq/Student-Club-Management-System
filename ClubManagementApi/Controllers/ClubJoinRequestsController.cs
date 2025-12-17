using ClubManagementApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ClubManagementApi.Controllers
{
    [Route("api/clubs/{clubId}/join-requests")]
    [ApiController]
    public class ClubJoinRequestsController : ControllerBase
    {
        private readonly StudentClubContext _context;

        public ClubJoinRequestsController(StudentClubContext context)
        {
            _context = context;
        }

        private int CurrentUserId => GetUserIdFromHttpContext(HttpContext);
        private string CurrentUserFullName => HttpContext.User.FindFirst("FullName")?.Value ?? "Người dùng";

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


        public class CreateJoinRequestDto
        {
            [Required(ErrorMessage = "Mã sinh viên là bắt buộc")]
            [StringLength(20, MinimumLength = 5, ErrorMessage = "Mã sinh viên phải từ 5 đến 20 ký tự")]
            public string StudentId { get; set; } = string.Empty;

            [Required(ErrorMessage = "Ngành học là bắt buộc")]
            [StringLength(200, MinimumLength = 2)]
            public string Major { get; set; } = string.Empty;

            [Required(ErrorMessage = "Năm học là bắt buộc")]
            [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "Năm học phải định dạng YYYY-YYYY (ví dụ: 2023-2024)")]
            public string AcademicYear { get; set; } = string.Empty;

            [Required(ErrorMessage = "Giới thiệu bản thân là bắt buộc")]
            [MinLength(20, ErrorMessage = "Giới thiệu phải ít nhất 20 ký tự")]
            public string Introduction { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lý do tham gia là bắt buộc")]
            [MinLength(20, ErrorMessage = "Lý do phải ít nhất 20 ký tự")]
            public string Reason { get; set; } = string.Empty;

            [StringLength(200)]
            public string? ContactInfoOptional { get; set; }
        }

        public class PaginationParams
        {
            [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn 0")]
            public int PageNumber { get; set; } = 1;

            [Range(1, 100, ErrorMessage = "Kích thước trang phải từ 1 đến 100")]
            public int PageSize { get; set; } = 10;
        }

        public class JoinRequestDto
        {
            public int RequestId { get; set; }
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string StudentId { get; set; } = string.Empty;
            public string Major { get; set; } = string.Empty;
            public string AcademicYear { get; set; } = string.Empty;
            public string Introduction { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string? ContactInfoOptional { get; set; }
            public string? Status { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private IActionResult ValidationErrorResponse()
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new { Field = x.Key, Message = x.Value?.Errors.First().ErrorMessage })
                .ToList();

            return BadRequest(ApiResponse<object>.FailResponse("Dữ liệu không hợp lệ", errors));
        }


        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<IActionResult> Create(int clubId, [FromBody] CreateJoinRequestDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var club = await _context.Clubs
                .FirstOrDefaultAsync(c => c.ClubId == clubId && c.Status == "Active");

            if (club == null)
                return BadRequest(ApiResponse<object>.FailResponse("CLB không tồn tại hoặc chưa được duyệt"));

            var isAlreadyMember = await _context.ClubMembers
                .AnyAsync(m => m.ClubId == clubId && m.UserId == CurrentUserId && m.Status == "Approved");

            if (isAlreadyMember)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn đã là thành viên của CLB này"));

            var existingRequest = await _context.ClubJoinRequests
                .AnyAsync(r => r.ClubId == clubId && r.UserId == CurrentUserId && r.Status == "Pending");

            if (existingRequest)
                return BadRequest(ApiResponse<object>.FailResponse("Bạn đã gửi yêu cầu tham gia, đang chờ duyệt"));

            var request = new ClubJoinRequest
            {
                UserId = CurrentUserId,
                ClubId = clubId,
                StudentId = dto.StudentId.Trim(),
                Major = dto.Major.Trim(),
                AcademicYear = dto.AcademicYear.Trim(),
                Introduction = dto.Introduction.Trim(),
                Reason = dto.Reason.Trim(),
                ContactInfoOptional = dto.ContactInfoOptional?.Trim(),
                Status = "Pending",
                CreatedAt = TimeZoneHelper.NowInVietnam
            };

            _context.ClubJoinRequests.Add(request);
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                var president = await _context.Clubs
                    .Where(c => c.ClubId == clubId)
                    .Select(c => c.President)
                    .FirstOrDefaultAsync();

                if (president != null)
                {
                    await NotificationService.SendAsync(
                        president.UserId,
                        "Yêu cầu tham gia CLB mới",
                        $"Sinh viên {CurrentUserFullName} (MSSV: {dto.StudentId}) muốn tham gia CLB {club.ClubName}"
                    );
                }
            });

            return Ok(ApiResponse<object>.SuccessResponse(
                new { request.RequestId },
                "Gửi yêu cầu tham gia thành công! Đang chờ chủ tịch duyệt."
            ));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetByClub(int clubId, [FromQuery] PaginationParams p, [FromQuery] string? status = null)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var club = await _context.Clubs.FindAsync(clubId);
            if (club == null) return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            bool canView = User.IsInRole("Admin") || club.PresidentId == CurrentUserId;
            if (!canView)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xem danh sách yêu cầu của CLB này"));

            var query = _context.ClubJoinRequests
                .Include(r => r.User)
                .Where(r => r.ClubId == clubId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);
            else
                query = query.Where(r => r.Status == "Pending" || r.Status == "Approved" || r.Status == "Rejected");

            var total = await query.CountAsync();

            var requests = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .Select(r => new JoinRequestDto
                {
                    RequestId = r.RequestId,
                    UserId = r.UserId,
                    FullName = r.User.FullName,
                    StudentId = r.StudentId,
                    Major = r.Major,
                    AcademicYear = r.AcademicYear,
                    Introduction = r.Introduction,
                    Reason = r.Reason,
                    ContactInfoOptional = r.ContactInfoOptional,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt.Value.ConvertToVietnamTime()
                })
                .ToListAsync();

            return Ok(ApiResponse<List<JoinRequestDto>>.SuccessResponse(requests, "Danh sách yêu cầu tham gia CLB", total));
        }

        [Authorize(Roles = "ClubLeader,Admin")]
        [HttpPatch("{requestId}/approve")]
        public async Task<IActionResult> Approve(int clubId, int requestId)
        {
            var request = await _context.ClubJoinRequests
                .Include(r => r.Club)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && r.ClubId == clubId);

            if (request == null) return NotFound(ApiResponse<object>.FailResponse("Yêu cầu không tồn tại"));

            if (request.Status != "Pending")
                return BadRequest(ApiResponse<object>.FailResponse("Yêu cầu này đã được xử lý"));

            bool canApprove = User.IsInRole("Admin") || request.Club.PresidentId == CurrentUserId;
            if (!canApprove)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền duyệt yêu cầu này"));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                request.Status = "Approved";
                request.ApprovedAt = TimeZoneHelper.NowInVietnam;

                var member = new ClubMember
                {
                    ClubId = clubId,
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
                throw;
            }

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(
                    request.UserId,
                    "Yêu cầu tham gia CLB được chấp nhận!",
                    $"Chúc mừng! Bạn đã chính thức trở thành thành viên của CLB \"{request.Club.ClubName}\""
                );

                var otherMembers = await _context.ClubMembers
                    .Where(m => m.ClubId == clubId && m.Status == "Approved" && m.UserId != request.UserId)
                    .Select(m => m.UserId)
                    .ToListAsync();

                if (otherMembers.Any())
                {
                    await NotificationService.SendToManyAsync(
                        otherMembers,
                        "Thành viên mới gia nhập CLB",
                        $"{request.User.FullName} vừa gia nhập CLB {request.Club.ClubName}"
                    );
                }
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Duyệt yêu cầu tham gia thành công"));
        }

        [Authorize(Roles = "ClubLeader,Admin")]
        [HttpPatch("{requestId}/reject")]
        public async Task<IActionResult> Reject(int clubId, int requestId)
        {
            var request = await _context.ClubJoinRequests
                .Include(r => r.Club)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && r.ClubId == clubId);

            if (request == null) return NotFound(ApiResponse<object>.FailResponse("Yêu cầu không tồn tại"));

            if (request.Status != "Pending")
                return BadRequest(ApiResponse<object>.FailResponse("Yêu cầu này đã được xử lý"));

            bool canReject = User.IsInRole("Admin") || request.Club.PresidentId == CurrentUserId;
            if (!canReject)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền từ chối yêu cầu này"));

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(
                    request.UserId,
                    "Yêu cầu tham gia CLB bị từ chối",
                    $"Rất tiếc, yêu cầu tham gia CLB \"{request.Club.ClubName}\" của bạn đã bị từ chối."
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Từ chối yêu cầu tham gia thành công"));
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