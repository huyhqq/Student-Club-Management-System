using ClubManagementApi.Data;
using ClubManagementApi.DTO;
using ClubManagementApi.Helpers;
using ClubManagementApi.Models;
using ClubManagementApi.Params;
using ClubManagementApi.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Controllers
{
    [Route("api/clubs/{clubId}/join-requests")]
    [ApiController]
    public class ClubJoinRequestsController : ControllerBase
    {
        private readonly StudentClubContext _context;
        private readonly IValidator<CreateJoinRequestDto> _createValidator;
        private readonly IValidator<PaginationParams> _paginationValidator;

        public ClubJoinRequestsController(
            StudentClubContext context,
            IValidator<CreateJoinRequestDto> createValidator,
            IValidator<PaginationParams> paginationValidator)
        {
            _context = context;
            _createValidator = createValidator;
            _paginationValidator = paginationValidator;
        }

        private int CurrentUserId => JwtHelper.GetUserIdFromHttpContext(HttpContext);

        public record CreateJoinRequestDto(
            string StudentId,
            string Major,
            string AcademicYear,
            string Introduction,
            string Reason,
            string? ContactInfoOptional
        );

        public record JoinRequestDto(
            int RequestId,
            int UserId,
            string FullName,
            string StudentId,
            string Major,
            string AcademicYear,
            string Introduction,
            string Reason,
            string? ContactInfoOptional,
            string? Status,
            DateTime CreatedAt
        );

        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<IActionResult> Create(int clubId, [FromBody] CreateJoinRequestDto dto)
        {
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

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

            TaskHelper.FireAndForget(async () =>
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
                        $"Sinh viên {User.FindFirst("FullName")?.Value} muốn tham gia CLB {club.ClubName}"
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
            var validation = await _paginationValidator.ValidateAsync(p);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

            var club = await _context.Clubs.FindAsync(clubId);
            if (club == null) return NotFound();

            bool canView = User.IsInRole("Admin") ||
                           club.PresidentId == CurrentUserId;

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
                .Select(r => new JoinRequestDto(
                    r.RequestId,
                    r.UserId,
                    r.User.FullName,
                    r.StudentId,
                    r.Major,
                    r.AcademicYear,
                    r.Introduction,
                    r.Reason,
                    r.ContactInfoOptional,
                    r.Status,
                    r.CreatedAt.Value.ConvertToVietnamTime()
                ))
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

            if (request == null) return NotFound();
            if (request.Status != "Pending")
                return BadRequest(ApiResponse<object>.FailResponse("Yêu cầu này đã được xử lý"));

            bool canApprove = User.IsInRole("Admin") ||
                              request.Club.PresidentId == CurrentUserId;

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

            TaskHelper.FireAndForget(async () =>
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

            if (request == null) return NotFound();
            if (request.Status != "Pending")
                return BadRequest(ApiResponse<object>.FailResponse("Yêu cầu này đã được xử lý"));

            bool canReject = User.IsInRole("Admin") ||
                             request.Club.PresidentId == CurrentUserId;

            if (!canReject)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền từ chối yêu cầu này"));

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            TaskHelper.FireAndForget(async () =>
            {
                await NotificationService.SendAsync(
                    request.UserId,
                    "Yêu cầu tham gia CLB bị từ chối",
                    $"Rất tiếc, yêu cầu tham gia CLB \"{request.Club.ClubName}\" của bạn đã bị từ chối."
                );
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Từ chối yêu cầu tham gia thành công"));
        }
    }
}
