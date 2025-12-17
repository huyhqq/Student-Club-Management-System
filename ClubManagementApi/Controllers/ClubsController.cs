using ClubManagementApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ClubManagementApi.Controllers
{
    [Route("api/clubs")]
    [ApiController]
    public class ClubsController : ControllerBase
    {
        private readonly StudentClubContext _context;

        public ClubsController(StudentClubContext context)
        {
            _context = context;
        }

        private int CurrentUserId => GetUserIdFromHttpContext(HttpContext);

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

        public class PaginationParams
        {
            [Range(1, int.MaxValue, ErrorMessage = "Trang phải lớn hơn 0")]
            public int PageNumber { get; set; } = 1;

            [Range(1, 100, ErrorMessage = "Kích thước trang từ 1 đến 100")]
            public int PageSize { get; set; } = 10;

            public string? Search { get; set; }

            public string? SortBy { get; set; } 
            public string? SortOrder { get; set; }
        }

        public class CreateClubDto
        {
            [Required(ErrorMessage = "Tên CLB là bắt buộc")]
            [StringLength(100, MinimumLength = 5, ErrorMessage = "Tên CLB phải từ 5 đến 100 ký tự")]
            public string ClubName { get; set; } = string.Empty;

            [StringLength(1000)]
            public string? Description { get; set; }

            [Range(0, 10000000, ErrorMessage = "Phí tham gia không hợp lệ")]
            public decimal? JoinFee { get; set; }
        }

        public class UpdateClubDto
        {
            [Required(ErrorMessage = "Tên CLB là bắt buộc")]
            [StringLength(100, MinimumLength = 5)]
            public string ClubName { get; set; } = string.Empty;

            [StringLength(1000)]
            public string? Description { get; set; }

            [Range(0, 10000000)]
            public decimal? JoinFee { get; set; }
        }

        public class ClubPublicDto
        {
            public int ClubId { get; set; }
            public string ClubName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public int MemberCount { get; set; }
            public string PresidentName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public decimal? JoinFee { get; set; }
            public bool IsJoined { get; set; }
            public bool HasPendingRequest { get; set; }
        }

        public class ClubDetailDto
        {
            public int ClubId { get; set; }
            public string ClubName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string Status { get; set; } = string.Empty;
            public int PresidentId { get; set; }
            public string PresidentName { get; set; } = string.Empty;
            public int MemberCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public decimal? JoinFee { get; set; }
            public bool IsJoined { get; set; }
            public bool HasPendingRequest { get; set; }
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

        [AllowAnonymous]
        [HttpGet("public")]
        public async Task<IActionResult> GetPublicList([FromQuery] PaginationParams p)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var query = _context.Clubs
                .Include(c => c.President)
                .Include(c => c.ClubMembers)
                .Include(c => c.FeeSchedules)
                .Where(c => c.Status == "Active");

            if (!string.IsNullOrEmpty(p.Search))
                query = query.Where(c => c.ClubName.Contains(p.Search.Trim()));

            // Sort đơn giản
            query = (p.SortBy?.ToLower(), p.SortOrder?.ToLower()) switch
            {
                ("clubname", "asc") => query.OrderBy(c => c.ClubName),
                ("clubname", "desc") => query.OrderByDescending(c => c.ClubName),
                _ => query.OrderByDescending(c => c.CreatedAt) // mặc định
            };

            var total = await query.CountAsync();

            var clubs = await query
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .ToListAsync();

            var joinedClubIds = new HashSet<int>();
            var pendingClubIds = new HashSet<int>();

            if (User.Identity?.IsAuthenticated == true)
            {
                joinedClubIds = new HashSet<int>(await _context.ClubMembers
                    .Where(m => m.UserId == CurrentUserId && m.Status == "Approved")
                    .Select(m => m.ClubId)
                    .ToListAsync());

                pendingClubIds = new HashSet<int>(await _context.ClubJoinRequests
                    .Where(r => r.UserId == CurrentUserId && r.Status == "Pending")
                    .Select(r => r.ClubId)
                    .ToListAsync());
            }

            var result = clubs.Select(c => new ClubPublicDto
            {
                ClubId = c.ClubId,
                ClubName = c.ClubName,
                Description = c.Description,
                MemberCount = c.ClubMembers.Count(m => m.Status == "Approved"),
                PresidentName = c.President!.FullName,
                CreatedAt = c.CreatedAt.Value.ConvertToVietnamTime(),
                JoinFee = c.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee == true)?.Amount,
                IsJoined = joinedClubIds.Contains(c.ClubId),
                HasPendingRequest = pendingClubIds.Contains(c.ClubId)
            }).ToList();

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
                isJoined = await _context.ClubMembers.AnyAsync(m => m.ClubId == id && m.UserId == CurrentUserId && m.Status == "Approved");
                hasPendingRequest = await _context.ClubJoinRequests.AnyAsync(r => r.ClubId == id && r.UserId == CurrentUserId && r.Status == "Pending");
            }

            var dto = new ClubPublicDto
            {
                ClubId = club.ClubId,
                ClubName = club.ClubName,
                Description = club.Description,
                MemberCount = club.ClubMembers.Count(m => m.Status == "Approved"),
                PresidentName = club.President!.FullName,
                CreatedAt = club.CreatedAt.Value.ConvertToVietnamTime(),
                JoinFee = club.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee == true)?.Amount,
                IsJoined = isJoined,
                HasPendingRequest = hasPendingRequest
            };

            return Ok(ApiResponse<ClubPublicDto>.SuccessResponse(dto));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams p, [FromQuery] string? status = null)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var query = _context.Clubs
                .Include(c => c.President)
                .Include(c => c.ClubMembers)
                .Include(c => c.FeeSchedules)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(c => c.PresidentId == CurrentUserId);
            }

            if (!string.IsNullOrEmpty(status))
                query = query.Where(c => c.Status == status);

            if (!string.IsNullOrEmpty(p.Search))
                query = query.Where(c => c.ClubName.Contains(p.Search.Trim()));

            // Sort đơn giản
            query = (p.SortBy?.ToLower(), p.SortOrder?.ToLower()) switch
            {
                ("clubname", "asc") => query.OrderBy(c => c.ClubName),
                ("clubname", "desc") => query.OrderByDescending(c => c.ClubName),
                _ => query.OrderByDescending(c => c.CreatedAt)
            };

            var total = await query.CountAsync();

            var clubs = await query
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .ToListAsync();

            var joinedClubIds = new HashSet<int>();
            var pendingClubIds = new HashSet<int>();

            if (User.Identity?.IsAuthenticated == true)
            {
                joinedClubIds = new HashSet<int>(await _context.ClubMembers
                    .Where(m => m.UserId == CurrentUserId && m.Status == "Approved")
                    .Select(m => m.ClubId)
                    .ToListAsync());

                pendingClubIds = new HashSet<int>(await _context.ClubJoinRequests
                    .Where(r => r.UserId == CurrentUserId && r.Status == "Pending")
                    .Select(r => r.ClubId)
                    .ToListAsync());
            }

            var result = clubs.Select(c => new ClubDetailDto
            {
                ClubId = c.ClubId,
                ClubName = c.ClubName,
                Description = c.Description,
                Status = c.Status,
                PresidentId = c.PresidentId.Value,
                PresidentName = c.President!.FullName,
                MemberCount = c.ClubMembers.Count(m => m.Status == "Approved"),
                CreatedAt = c.CreatedAt.Value.ConvertToVietnamTime(),
                JoinFee = c.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee == true)?.Amount,
                IsJoined = joinedClubIds.Contains(c.ClubId),
                HasPendingRequest = pendingClubIds.Contains(c.ClubId)
            }).ToList();

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
            bool isMember = await _context.ClubMembers.AnyAsync(m => m.ClubId == id && m.UserId == CurrentUserId && m.Status == "Approved");

            if (!isAdmin && !isPresident && !isMember)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xem chi tiết CLB này"));

            bool hasPendingRequest = await _context.ClubJoinRequests.AnyAsync(r => r.ClubId == id && r.UserId == CurrentUserId && r.Status == "Pending");

            var dto = new ClubDetailDto
            {
                ClubId = club.ClubId,
                ClubName = club.ClubName,
                Description = club.Description,
                Status = club.Status,
                PresidentId = club.PresidentId.Value,
                PresidentName = club.President!.FullName,
                MemberCount = club.ClubMembers.Count(m => m.Status == "Approved"),
                CreatedAt = club.CreatedAt.Value.ConvertToVietnamTime(),
                JoinFee = club.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee == true)?.Amount,
                IsJoined = isMember,
                HasPendingRequest = hasPendingRequest
            };

            return Ok(ApiResponse<ClubDetailDto>.SuccessResponse(dto));
        }

        [Authorize(Roles = "Student,ClubLeader,Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateClubDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            if (await _context.Clubs.AnyAsync(c => EF.Functions.Collate(c.ClubName, "SQL_Latin1_General_CP1_CI_AS") == dto.ClubName.Trim()))
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

                // Tự động duyệt chủ nhiệm làm thành viên đầu tiên
                _context.ClubMembers.Add(new ClubMember
                {
                    ClubId = club.ClubId,
                    UserId = CurrentUserId,
                    Status = "Approved",
                    JoinedDate = TimeZoneHelper.NowInVietnam
                });

                if (dto.JoinFee.HasValue && dto.JoinFee > 0)
                {
                    _context.FeeSchedules.Add(new FeeSchedule
                    {
                        ClubId = club.ClubId,
                        FeeName = "Phí tham gia CLB",
                        Amount = dto.JoinFee.Value,
                        DueDate = DateOnly.FromDateTime(TimeZoneHelper.NowInVietnam.AddDays(365)),
                        Frequency = "OneTime",
                        Status = "Active",
                        CreatedAt = TimeZoneHelper.NowInVietnam,
                        IsRequiredFee = true
                    });
                }

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
                var admins = await _context.Users.Where(u => u.Role == "Admin").Select(u => u.UserId).ToListAsync();
                var userName = HttpContext.User.FindFirst("FullName")?.Value ?? "Một người dùng";

                foreach (var adminId in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = adminId,
                        Title = "Yêu cầu duyệt CLB mới",
                        Message = $"CLB \"{club.ClubName}\" do {userName} tạo cần được duyệt",
                        CreatedAt = TimeZoneHelper.NowInVietnam
                    });
                }
                await _context.SaveChangesAsync();
            });

            return Ok(ApiResponse<object>.SuccessResponse(new { club.ClubId }, "Tạo CLB thành công! Đang chờ Admin duyệt."));
        }

        [Authorize(Roles = "ClubLeader,Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateClubDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var club = await _context.Clubs
                .Include(c => c.FeeSchedules)
                .FirstOrDefaultAsync(c => c.ClubId == id);

            if (club == null) return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            bool isPresident = club.PresidentId == CurrentUserId;
            if (!isPresident && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền chỉnh sửa CLB này"));

            if (await _context.Clubs.AnyAsync(c => EF.Functions.Collate(c.ClubName, "SQL_Latin1_General_CP1_CI_AS") == dto.ClubName.Trim() && c.ClubId != id))
                return BadRequest(ApiResponse<object>.FailResponse("Tên CLB đã được sử dụng"));

            club.ClubName = dto.ClubName.Trim();
            club.Description = dto.Description?.Trim();

            var existingFee = club.FeeSchedules.FirstOrDefault(f => f.Frequency == "OneTime" && f.IsRequiredFee == true);

            if (dto.JoinFee.HasValue && dto.JoinFee > 0)
            {
                if (existingFee != null)
                {
                    existingFee.Amount = dto.JoinFee.Value;
                }
                else
                {
                    _context.FeeSchedules.Add(new FeeSchedule
                    {
                        ClubId = club.ClubId,
                        FeeName = "Phí tham gia CLB",
                        Amount = dto.JoinFee.Value,
                        DueDate = DateOnly.FromDateTime(TimeZoneHelper.NowInVietnam.AddDays(365)),
                        Frequency = "OneTime",
                        Status = "Active",
                        CreatedAt = TimeZoneHelper.NowInVietnam,
                        IsRequiredFee = true
                    });
                }
            }
            else if (existingFee != null)
            {
                _context.FeeSchedules.Remove(existingFee);
            }

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Cập nhật CLB thành công"));
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var club = await _context.Clubs.Include(c => c.President).FirstOrDefaultAsync(c => c.ClubId == id);
            if (club == null) return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            if (club.Status == "Active")
                return BadRequest(ApiResponse<object>.FailResponse("CLB đã được duyệt"));

            club.Status = "Active";
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(club.PresidentId!.Value, "CLB đã được duyệt!", $"CLB \"{club.ClubName}\" chính thức hoạt động!");
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Duyệt CLB thành công"));
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/suspend")]
        public async Task<IActionResult> Suspend(int id)
        {
            var club = await _context.Clubs.Include(c => c.President).FirstOrDefaultAsync(c => c.ClubId == id);
            if (club == null) return NotFound(ApiResponse<object>.FailResponse("CLB không tồn tại"));

            club.Status = "Suspended";
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                await NotificationService.SendAsync(club.PresidentId!.Value, "CLB bị đình chỉ", $"CLB \"{club.ClubName}\" đã bị đình chỉ hoạt động.");
            });

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đình chỉ CLB thành công"));
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