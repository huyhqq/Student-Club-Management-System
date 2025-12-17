using ClubManagementApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ClubManagementApi.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/admin/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly StudentClubContext _context;

        public UsersController(StudentClubContext context)
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

        // ==================== DTO VỚI DATA ANNOTATIONS ====================

        public class PaginationParams
        {
            [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn 0")]
            public int PageNumber { get; set; } = 1;

            [Range(1, 100, ErrorMessage = "Kích thước trang từ 1 đến 100")]
            public int PageSize { get; set; } = 10;

            public string? Search { get; set; }

            public string? SortBy { get; set; } // "CreatedAt" hoặc "FullName"
            public string? SortOrder { get; set; } // "asc" hoặc "desc"
        }

        public class CreateAdminUserDto
        {
            [Required(ErrorMessage = "Họ và tên là bắt buộc")]
            [StringLength(100, MinimumLength = 2)]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
            [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
            public string Password { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
            public string? Phone { get; set; }

            public string? StudentCode { get; set; }

            [Required(ErrorMessage = "Vai trò là bắt buộc")]
            [RegularExpression("^(Student|ClubLeader|Admin)$", ErrorMessage = "Vai trò chỉ có thể là Student, ClubLeader hoặc Admin")]
            public string Role { get; set; } = string.Empty;
        }

        public class UpdateAdminUserDto
        {
            [Required(ErrorMessage = "Họ và tên là bắt buộc")]
            [StringLength(100, MinimumLength = 2)]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Phone]
            public string? Phone { get; set; }

            public string? StudentCode { get; set; }

            public string? Avatar { get; set; }
        }

        public class UpdateUserRoleDto
        {
            [Required(ErrorMessage = "Vai trò là bắt buộc")]
            [RegularExpression("^(Student|ClubLeader|Admin)$", ErrorMessage = "Vai trò không hợp lệ")]
            public string Role { get; set; } = string.Empty;
        }

        public class UpdateUserStatusDto
        {
            [Required(ErrorMessage = "Trạng thái tài khoản là bắt buộc")]
            [RegularExpression("^(Active|Locked|Disabled|PendingVerification)$", ErrorMessage = "Trạng thái tài khoản không hợp lệ")]
            public string AccountStatus { get; set; } = string.Empty;
        }

        public class UserAdminDto
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Phone { get; set; }
            public string? StudentCode { get; set; }
            public string Role { get; set; } = string.Empty;
            public string AccountStatus { get; set; } = string.Empty;
            public string? Avatar { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastLogin { get; set; }
        }

        // ==================== HÀM HỖ TRỢ ====================

        private IActionResult ValidationErrorResponse()
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new { Field = x.Key, Message = x.Value?.Errors.First().ErrorMessage })
                .ToList();

            return BadRequest(ApiResponse<object>.FailResponse("Dữ liệu không hợp lệ", errors));
        }

        // ==================== CÁC ENDPOINT ====================

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams p)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(p.Search))
            {
                var search = p.Search.Trim();
                query = query.Where(u =>
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search) ||
                    (u.StudentCode != null && u.StudentCode.Contains(search)));
            }

            // Sort đơn giản
            query = (p.SortBy?.ToLower(), p.SortOrder?.ToLower()) switch
            {
                ("fullname", "asc") => query.OrderBy(u => u.FullName),
                ("fullname", "desc") => query.OrderByDescending(u => u.FullName),
                _ => query.OrderByDescending(u => u.CreatedAt)
            };

            var total = await query.CountAsync();

            var users = await query
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .Select(u => new UserAdminDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    StudentCode = u.StudentCode,
                    Role = u.Role,
                    AccountStatus = u.AccountStatus,
                    Avatar = u.Avatar,
                    CreatedAt = u.CreatedAt.Value.ConvertToVietnamTime(),
                    LastLogin = u.LastLogin.HasValue ? u.LastLogin.Value.ConvertToVietnamTime() : null
                })
                .ToListAsync();

            return Ok(ApiResponse<List<UserAdminDto>>.SuccessResponse(users, "Lấy danh sách người dùng thành công", total));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            var dto = new UserAdminDto
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                StudentCode = user.StudentCode,
                Role = user.Role,
                AccountStatus = user.AccountStatus,
                Avatar = user.Avatar,
                CreatedAt = user.CreatedAt.Value.ConvertToVietnamTime(),
                LastLogin = user.LastLogin.HasValue ? user.LastLogin.Value.ConvertToVietnamTime() : null
            };

            return Ok(ApiResponse<UserAdminDto>.SuccessResponse(dto));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAdminUserDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            if (await _context.Users.AnyAsync(u => u.Email.Trim() == dto.Email.Trim()))
                return BadRequest(ApiResponse<object>.FailResponse("Email đã được sử dụng"));

            if (!string.IsNullOrEmpty(dto.StudentCode) &&
                await _context.Users.AnyAsync(u => u.StudentCode == dto.StudentCode.Trim()))
                return BadRequest(ApiResponse<object>.FailResponse("Mã sinh viên đã được sử dụng"));

            var hashed = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            var passwordHashBytes = Encoding.UTF8.GetBytes(hashed);

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = dto.Email.Trim(),
                Phone = dto.Phone?.Trim(),
                StudentCode = dto.StudentCode?.Trim(),
                PasswordHash = passwordHashBytes,
                Role = dto.Role,
                AccountStatus = "Active",
                CreatedAt = TimeZoneHelper.NowInVietnam
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new { user.UserId }, "Tạo tài khoản thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAdminUserDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            if (user.UserId == CurrentUserId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể cập nhật thông tin chính mình qua API này"));

            if (await _context.Users.AnyAsync(u => u.Email.Trim() == dto.Email.Trim() && u.UserId != id))
                return BadRequest(ApiResponse<object>.FailResponse("Email đã được sử dụng"));

            if (!string.IsNullOrEmpty(dto.StudentCode) &&
                await _context.Users.AnyAsync(u => u.StudentCode == dto.StudentCode.Trim() && u.UserId != id))
                return BadRequest(ApiResponse<object>.FailResponse("Mã sinh viên đã được sử dụng"));

            user.FullName = dto.FullName.Trim();
            user.Email = dto.Email.Trim();
            user.Phone = dto.Phone?.Trim();
            user.StudentCode = dto.StudentCode?.Trim();
            user.Avatar = dto.Avatar?.Trim();

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Cập nhật thông tin người dùng thành công"));
        }

        [HttpPatch("{id}/role")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateUserRoleDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            if (user.UserId == CurrentUserId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể thay đổi vai trò của chính mình"));

            user.Role = dto.Role;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Cập nhật vai trò thành công"));
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateUserStatusDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            if (user.UserId == CurrentUserId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể thay đổi trạng thái của chính mình"));

            user.AccountStatus = dto.AccountStatus;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Cập nhật trạng thái tài khoản thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            if (user.Role == "Admin")
                return StatusCode(403, ApiResponse<object>.FailResponse("Không thể vô hiệu hóa tài khoản Admin"));

            if (user.UserId == CurrentUserId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể vô hiệu hóa tài khoản của chính mình"));

            user.AccountStatus = "Disabled";
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Vô hiệu hóa tài khoản thành công"));
        }
    }
}