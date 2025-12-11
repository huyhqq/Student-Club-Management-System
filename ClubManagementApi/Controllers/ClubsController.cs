using ClubManagementApi.Controllers;
using ClubManagementApi.Data;
using ClubManagementApi.DTO;
using ClubManagementApi.Helpers;
using ClubManagementApi.Models;
using ClubManagementApi.Params;
using ClubManagementApi.Services;
using ClubManagementApi.Validator;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace ClubManagementApi.Controllers
{
    [Route("api/clubs")]
    [ApiController]
    public class ClubsController : ControllerBase
    {
        private readonly StudentClubContext _context;
        private readonly IValidator<CreateClubDto> _createValidator;
        private readonly IValidator<UpdateClubDto> _updateValidator;
        private readonly IValidator<PaginationParams> _paginationValidator;
        public ClubsController(
            StudentClubContext context,
            IValidator<CreateClubDto> createValidator,
            IValidator<UpdateClubDto> updateValidator,
            IValidator<PaginationParams> paginationValidator)
        {
            _context = context;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _paginationValidator = paginationValidator;
        }
        private int CurrentUserId => JwtHelper.GetUserIdFromHttpContext(HttpContext);
        public record ClubPublicDto(
            int ClubId,
            string ClubName,
            string? Description,
            int MemberCount,
            string PresidentName,
            DateTime CreatedAt,
            decimal? JoinFee,
            bool IsJoined,
            bool HasPendingRequest
        );
        public record ClubDetailDto(
            int ClubId,
            string ClubName,
            string? Description,
            string Status,
            int PresidentId,
            string PresidentName,
            int MemberCount,
            DateTime CreatedAt,
            decimal? JoinFee,
            bool IsJoined,
            bool HasPendingRequest
        );
        public record CreateClubDto(string ClubName, string? Description, decimal? JoinFee);
        public record UpdateClubDto(string ClubName, string? Description, decimal? JoinFee);

        [AllowAnonymous]
        [HttpGet("public")]
        public async Task<IActionResult> GetPublicList([FromQuery] PaginationParams p)
        {
            var validation = await _paginationValidator.ValidateAsync(p);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

            var query = _context.Clubs
                .Include(c => c.President)
                .Include(c => c.ClubMembers)
                .Include(c => c.FeeSchedules)
                .Where(c => c.Status == "Active");

            if (!string.IsNullOrEmpty(p.Search))
                query = query.Where(c => c.ClubName.Contains(p.Search.Trim()));

            var total = await query.CountAsync();

            var clubsQuery = query
                .OrderByDynamic(p.SortBy ?? "CreatedAt", p.SortOrder ?? "desc")
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize);

            var clubs = await clubsQuery.ToListAsync();

            var joinedClubIds = new HashSet<int>();
            var pendingClubIds = new HashSet<int>();

            if (User.Identity?.IsAuthenticated == true)
            {
                var joinedList = await _context.ClubMembers
                    .Where(m => m.UserId == CurrentUserId && m.Status == "Approved")
                    .Select(m => m.ClubId)
                    .ToListAsync();
                joinedClubIds = new HashSet<int>(joinedList);

                var pendingList = await _context.ClubJoinRequests
                    .Where(r => r.UserId == CurrentUserId && r.Status == "Pending")
                    .Select(r => r.ClubId)
                    .ToListAsync();
                pendingClubIds = new HashSet<int>(pendingList);
            }

            var result = clubs.Select(c => new ClubPublicDto(
                c.ClubId,
                c.ClubName,
                c.Description,
                c.ClubMembers.Count(m => m.Status == "Approved"),
                c.President!.FullName,
                c.CreatedAt.Value.ConvertToVietnamTime(),
                c.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee)?.Amount,
                joinedClubIds.Contains(c.ClubId),
                pendingClubIds.Contains(c.ClubId)
            )).ToList();

            return Ok(ApiResponse<List<ClubPublicDto>>.SuccessResponse(result, "Danh sách CLB công khai", total));
        }

        [AllowAnonymous]
        [HttpGet("public/{id}")]
        public async Task<IActionResult> GetPublicById(int id)
        {
            var club = await _context.Clubs
                .Include(c => c.President)
                .Include(c => c.ClubMembers)
                .Include(c => c.FeeSchedules)
                .FirstOrDefaultAsync(c => c.ClubId == id && c.Status == "Active");

            if (club == null)
                return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại hoặc chưa được duyệt"));

            bool isJoined = false;
            bool hasPendingRequest = false;

            if (User.Identity?.IsAuthenticated == true)
            {
                isJoined = await _context.ClubMembers
                    .AnyAsync(m => m.ClubId == id && m.UserId == CurrentUserId && m.Status == "Approved");

                hasPendingRequest = await _context.ClubJoinRequests
                    .AnyAsync(r => r.ClubId == id && r.UserId == CurrentUserId && r.Status == "Pending");
            }

            var dto = new ClubPublicDto(
                club.ClubId,
                club.ClubName,
                club.Description,
                club.ClubMembers.Count(m => m.Status == "Approved"),
                club.President!.FullName,
                club.CreatedAt.Value.ConvertToVietnamTime(),
                club.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee)?.Amount,
                isJoined,
                hasPendingRequest
            );

            return Ok(ApiResponse<ClubPublicDto>.SuccessResponse(dto));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams p, [FromQuery] string? status = null)
        {
            var validation = await _paginationValidator.ValidateAsync(p);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

            var query = _context.Clubs
                .Include(c => c.President)
                .Include(c => c.ClubMembers)
                .Include(c => c.FeeSchedules)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(c =>
                    c.PresidentId == CurrentUserId &&
                    (c.Status == "Active" || c.Status == "Pending")
                );
            }

            if (!string.IsNullOrEmpty(status))
                query = query.Where(c => c.Status == status);

            if (!string.IsNullOrEmpty(p.Search))
                query = query.Where(c => c.ClubName.Contains(p.Search.Trim()));

            var total = await query.CountAsync();

            var clubs = await query
                .OrderByDynamic(p.SortBy ?? "CreatedAt", p.SortOrder ?? "desc")
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .ToListAsync();

            var joinedClubIds = new HashSet<int>();
            var pendingClubIds = new HashSet<int>();

            if (User.Identity?.IsAuthenticated == true)
            {
                joinedClubIds = await _context.ClubMembers
                    .Where(m => m.UserId == CurrentUserId && m.Status == "Approved")
                    .Select(m => m.ClubId)
                    .ToListAsync()
                    .ContinueWith(t => new HashSet<int>(t.Result));

                pendingClubIds = await _context.ClubJoinRequests
                    .Where(r => r.UserId == CurrentUserId && r.Status == "Pending")
                    .Select(r => r.ClubId)
                    .ToListAsync()
                    .ContinueWith(t => new HashSet<int>(t.Result));
            }

            var result = clubs.Select(c => new ClubDetailDto(
                c.ClubId,
                c.ClubName,
                c.Description,
                c.Status,
                c.PresidentId.Value,
                c.President!.FullName,
                c.ClubMembers.Count(m => m.Status == "Approved"),
                c.CreatedAt.Value.ConvertToVietnamTime(),
                c.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee)?.Amount,
                joinedClubIds.Contains(c.ClubId),
                pendingClubIds.Contains(c.ClubId)
            )).ToList();

            return Ok(ApiResponse<List<ClubDetailDto>>.SuccessResponse(result, "Danh sách CLB", total));
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var club = await _context.Clubs
                .Include(c => c.President)
                .Include(c => c.ClubMembers)
                .Include(c => c.FeeSchedules)
                .FirstOrDefaultAsync(c => c.ClubId == id);

            if (club == null)
                return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            bool isPresident = club.PresidentId == CurrentUserId;
            bool isAdmin = User.IsInRole("Admin");
            bool isMember = await _context.ClubMembers
                .AnyAsync(m => m.ClubId == id && m.UserId == CurrentUserId && m.Status == "Approved");

            if (!isAdmin && !isPresident && !isMember)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xem CLB này"));

            bool hasPendingRequest = await _context.ClubJoinRequests
                .AnyAsync(r => r.ClubId == id && r.UserId == CurrentUserId && r.Status == "Pending");

            var dto = new ClubDetailDto(
                club.ClubId,
                club.ClubName,
                club.Description,
                club.Status,
                club.PresidentId.Value,
                club.President!.FullName,
                club.ClubMembers.Count(m => m.Status == "Approved"),
                club.CreatedAt.Value.ConvertToVietnamTime(),
                club.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee)?.Amount,
                isMember,
                hasPendingRequest
            );

            return Ok(ApiResponse<ClubDetailDto>.SuccessResponse(dto));
        }

        [Authorize(Roles = "Student,ClubLeader")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateClubDto dto)
        {
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));
            if (await _context.Clubs.AnyAsync(c => c.ClubName.Trim() == dto.ClubName.Trim()))
                return BadRequest(ApiResponse<object>.FailResponse("Tên CLB đã tồn tại"));
            var club = new Club
            {
                ClubName = dto.ClubName.Trim(),
                Description = dto.Description?.Trim(),
                PresidentId = CurrentUserId,
                Status = "Pending",
                CreatedAt = TimeZoneHelper.NowInVietnam
            };
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Clubs.Add(club);
                await _context.SaveChangesAsync();

                var joinRequest = new ClubJoinRequest
                {
                    ClubId = club.ClubId,
                    UserId = CurrentUserId,
                    Status = "Approved",
                    CreatedAt = TimeZoneHelper.NowInVietnam,
                    StudentId = "CLUBLEADER",
                    Major = "CLUBLEADER",
                    AcademicYear = DateTime.Now.Year.ToString(),
                    Introduction = "CLUBLEADER",
                    Reason = "CLUBLEADER",
                    ContactInfoOptional = null
                };

                await _context.SaveChangesAsync();
                _context.ClubJoinRequests.Add(joinRequest);
                _context.ClubMembers.Add(new ClubMember
                {
                    ClubId = club.ClubId,
                    UserId = CurrentUserId,
                    Status = "Approved",
                    JoinedDate = TimeZoneHelper.NowInVietnam
                });
                await _context.SaveChangesAsync();

                if (dto.JoinFee.HasValue && dto.JoinFee > 0)
                {
                    var existingFee = await _context.FeeSchedules
                        .FirstOrDefaultAsync(f => f.ClubId == club.ClubId && f.Frequency == "OneTime" && f.IsRequiredFee);

                    if (existingFee != null)
                    {
                        existingFee.Amount = dto.JoinFee.Value;
                        _context.FeeSchedules.Update(existingFee);
                    }
                    else
                    {
                        var newFee = new FeeSchedule
                        {
                            ClubId = club.ClubId,
                            FeeName = "Phí tham gia CLB",
                            Amount = dto.JoinFee.Value,
                            DueDate = DateOnly.FromDateTime(TimeZoneHelper.NowInVietnam.AddDays(30)),
                            Frequency = "OneTime",
                            Status = "Active",
                            CreatedAt = TimeZoneHelper.NowInVietnam,
                            IsRequiredFee = true
                        };
                        _context.FeeSchedules.Add(newFee);
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            TaskHelper.FireAndForget(async () =>
            {
                var admins = await _context.Users.Where(u => u.Role == "Admin").Select(u => u.UserId).ToListAsync();
                foreach (var adminId in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = adminId,
                        Title = "Yêu cầu duyệt CLB mới",
                        Message = $"CLB \"{club.ClubName}\" do {User.FindFirst("FullName")?.Value} tạo cần được duyệt",
                        CreatedAt = TimeZoneHelper.NowInVietnam
                    });
                }
                await _context.SaveChangesAsync();
            });
            return Ok(ApiResponse<object>.SuccessResponse(
                new { club.ClubId },
                "Tạo CLB thành công! Đang chờ Admin duyệt."
            ));
        }

        [Authorize(Roles = "ClubLeader,Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateClubDto dto)
        {
            var validation = await _updateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));
            var club = await _context.Clubs
                .Include(c => c.FeeSchedules)
                .FirstOrDefaultAsync(c => c.ClubId == id);
            if (club == null) return NotFound();
            bool isPresident = club.PresidentId == CurrentUserId;
            if (!isPresident && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không phải chủ tịch CLB này"));
            if (await _context.Clubs.AnyAsync(c => c.ClubName.Trim() == dto.ClubName.Trim() && c.ClubId != id))
                return BadRequest(ApiResponse<object>.FailResponse("Tên CLB đã được sử dụng"));
            club.ClubName = dto.ClubName.Trim();
            club.Description = dto.Description?.Trim();

            if (dto.JoinFee.HasValue)
            {
                var existingFee = club.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee);

                if (existingFee != null)
                {
                    if (dto.JoinFee.Value > 0)
                    {
                        existingFee.Amount = dto.JoinFee.Value;
                        _context.FeeSchedules.Update(existingFee);
                    }
                    else
                    {
                        _context.FeeSchedules.Remove(existingFee);
                    }
                }
                else if (dto.JoinFee.Value > 0)
                {
                    var newFee = new FeeSchedule
                    {
                        ClubId = club.ClubId,
                        FeeName = "Phí tham gia CLB",
                        Amount = dto.JoinFee.Value,
                        DueDate = DateOnly.FromDateTime(TimeZoneHelper.NowInVietnam.AddDays(30)),
                        Frequency = "OneTime",
                        Status = "Active",
                        CreatedAt = TimeZoneHelper.NowInVietnam,
                        IsRequiredFee = true
                    };
                    _context.FeeSchedules.Add(newFee);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(ApiResponse<string>.SuccessResponse(null, "Cập nhật CLB thành công"));
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var club = await _context.Clubs.Include(c => c.President).FirstOrDefaultAsync(c => c.ClubId == id);
            if (club == null) return NotFound();
            if (club.Status == "Active") return BadRequest(ApiResponse<object>.FailResponse("CLB đã được duyệt rồi"));
            club.Status = "Active";
            await _context.SaveChangesAsync();
            TaskHelper.FireAndForget(async () =>
                await NotificationService.SendAsync(club.PresidentId.Value, "CLB đã được duyệt!", $"CLB \"{club.ClubName}\" chính thức hoạt động!")
            );
            return Ok(ApiResponse<string>.SuccessResponse(null, "Duyệt CLB thành công"));
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/suspend")]
        public async Task<IActionResult> Suspend(int id)
        {
            var club = await _context.Clubs.Include(c => c.President).FirstOrDefaultAsync(c => c.ClubId == id);
            if (club == null) return NotFound();
            club.Status = "Suspended";
            await _context.SaveChangesAsync();
            TaskHelper.FireAndForget(async () =>
                await NotificationService.SendAsync(club.PresidentId.Value, "CLB bị đình chỉ", $"CLB \"{club.ClubName}\" đã bị đình chỉ hoạt động.")
            );
            return Ok(ApiResponse<string>.SuccessResponse(null, "Đình chỉ CLB thành công"));
        }
    }
}
