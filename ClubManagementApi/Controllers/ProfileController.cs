using System.Text;
using BCrypt.Net;
using ClubManagementApi.Data;
using ClubManagementApi.DTO;
using ClubManagementApi.Helpers;
using ClubManagementApi.Services;
using ClubManagementApi.Validator;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubManagementApi.Controllers
{
    [Authorize]
    [Route("api/profile")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly StudentClubContext _context;
        private readonly CloudinaryService _cloudinaryService;
        private readonly IValidator<UpdateProfileDto> _updateValidator;

        public ProfileController(
            StudentClubContext context,
            CloudinaryService cloudinaryService,
            IValidator<UpdateProfileDto> updateValidator)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
            _updateValidator = updateValidator;
        }

        private int CurrentUserId => JwtHelper.GetUserIdFromHttpContext(HttpContext);

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == CurrentUserId);

            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            var response = new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.Phone,
                Avatar = user.Avatar ?? "https://res.cloudinary.com/your-cloud/image/upload/v1/default-avatar.png",
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
            var validation = await _updateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

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
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest(ApiResponse<object>.FailResponse("Vui lòng chọn ảnh"));

            if (dto.File.Length > 5 * 1024 * 1024)
                return BadRequest(ApiResponse<object>.FailResponse("Ảnh không được vượt quá 5MB"));

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(dto.File.ContentType.ToLower()))
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
            var validator = new ChangePasswordDtoValidator();
            var result = await validator.ValidateAsync(dto);
            if (!result.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(result.Errors.First().ErrorMessage));

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(ApiResponse<object>.FailResponse("Mật khẩu mới và xác nhận mật khẩu không khớp"));

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Người dùng không tồn tại"));

            var storedHash = Encoding.UTF8.GetString(user.PasswordHash);
            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, storedHash))
                return BadRequest(ApiResponse<object>.FailResponse("Mật khẩu hiện tại không đúng"));
            var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.PasswordHash = Encoding.UTF8.GetBytes(newHash); await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đổi mật khẩu thành công"));
        }

        public record UpdateProfileDto(string FullName, string? Phone);
        public record UpdateAvatarDto(IFormFile File);
        public record ChangePasswordDto(string CurrentPassword, string NewPassword, string ConfirmPassword);
    }
}
