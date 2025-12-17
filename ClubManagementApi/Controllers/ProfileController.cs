using ClubManagementApi.Models;
using ClubManagementApi.Services;
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
    [Authorize]
    [Route("api/profile")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly StudentClubContext _context;
        private readonly CloudinaryService _cloudinaryService;

        public ProfileController(StudentClubContext context, CloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
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

        public class UpdateProfileDto
        {
            [Required(ErrorMessage = "Họ và tên là bắt buộc")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 100 ký tự")]
            public string FullName { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
            [StringLength(20)]
            public string? Phone { get; set; }
        }

        public class UpdateAvatarDto
        {
            [Required(ErrorMessage = "Vui lòng chọn file ảnh")]
            public IFormFile File { get; set; } = null!;
        }

        public class ChangePasswordDto
        {
            [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
            [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự")]
            public string NewPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
            [Compare("NewPassword", ErrorMessage = "Mật khẩu mới và xác nhận không khớp")]
            public string ConfirmPassword { get; set; } = string.Empty;
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
        public async Task<IActionResult> Get()
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == CurrentUserId);

            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            var defaultAvatar = "https://res.cloudinary.com/your-cloud/image/upload/v1/default-avatar.png";

            var response = new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.Phone,
                Avatar = user.Avatar ?? defaultAvatar,
                user.Role,
                user.AccountStatus,
                CreatedAt = user.CreatedAt?.ConvertToVietnamTime(),
                LastLogin = user.LastLogin?.ConvertToVietnamTime()
            };

            return Ok(ApiResponse<object>.SuccessResponse(response, "Lấy thông tin thành công"));
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateProfileDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Người dùng không tồn tại"));

            user.FullName = dto.FullName.Trim();
            user.Phone = dto.Phone?.Trim();

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Cập nhật hồ sơ thành công"));
        }

        [HttpPut("avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateAvatar([FromForm] UpdateAvatarDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            if (dto.File == null || dto.File.Length == 0)
                return BadRequest(ApiResponse<object>.FailResponse("Vui lòng chọn ảnh"));

            if (dto.File.Length > 5 * 1024 * 1024)
                return BadRequest(ApiResponse<object>.FailResponse("Ảnh không được vượt quá 5MB"));

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(dto.File.ContentType.ToLowerInvariant()))
                return BadRequest(ApiResponse<object>.FailResponse("Chỉ chấp nhận định dạng JPG, PNG, WEBP"));

            try
            {
                var uploadResult = await _cloudinaryService.UploadAsync(dto.File, "club-avatars");

                var user = await _context.Users.FindAsync(CurrentUserId);
                if (user == null)
                    return NotFound(ApiResponse<object>.FailResponse("Người dùng không tồn tại"));

                user.Avatar = uploadResult;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResponse(
                    new { Avatar = uploadResult },
                    "Cập nhật ảnh đại diện thành công"
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.FailResponse("Lỗi upload ảnh: " + ex.Message));
            }
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Người dùng không tồn tại"));

            var storedHash = Encoding.UTF8.GetString(user.PasswordHash);
            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, storedHash))
                return BadRequest(ApiResponse<object>.FailResponse("Mật khẩu hiện tại không đúng"));

            var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.PasswordHash = Encoding.UTF8.GetBytes(newHash);

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đổi mật khẩu thành công"));
        }
    }
}