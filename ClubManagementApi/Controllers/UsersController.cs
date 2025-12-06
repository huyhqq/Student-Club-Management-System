using ClubManagementApi.Data;
using ClubManagementApi.DTO;
using ClubManagementApi.Helpers;
using ClubManagementApi.Models;
using ClubManagementApi.Params;
using ClubManagementApi.Validator;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ClubManagementApi.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/admin/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly StudentClubContext _context;
        private readonly IValidator<PaginationParams> _paginationValidator;
        private readonly IValidator<UpdateUserRoleDto> _roleValidator;
        private readonly IValidator<UpdateUserStatusDto> _statusValidator;
        private readonly IValidator<CreateAdminUserDto> _createValidator;
        private readonly IValidator<UpdateAdminUserDto> _updateValidator;

        public UsersController(
            StudentClubContext context,
            IValidator<PaginationParams> paginationValidator,
            IValidator<UpdateUserRoleDto> roleValidator,
            IValidator<UpdateUserStatusDto> statusValidator,
            IValidator<CreateAdminUserDto> createValidator,
            IValidator<UpdateAdminUserDto> updateValidator)
        {
            _context = context;
            _paginationValidator = paginationValidator;
            _roleValidator = roleValidator;
            _statusValidator = statusValidator;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
        }

        private int CurrentUserId => JwtHelper.GetUserIdFromHttpContext(HttpContext);

        public record UserAdminDto(
            int UserId,
            string FullName,
            string Email,
            string? Phone,
            string? StudentCode,
            string Role,
            string AccountStatus,
            string? Avatar,
            DateTime CreatedAt,
            DateTime? LastLogin
        );

        public record CreateAdminUserDto(
            string FullName,
            string Email,
            string Password,
            string? Phone,
            string? StudentCode,
            string Role
        );

        public record UpdateAdminUserDto(
            string FullName,
            string Email,
            string? Phone,
            string? StudentCode,
            string? Avatar
        );

        public record UpdateUserRoleDto(string Role);
        public record UpdateUserStatusDto(string AccountStatus);

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams p)
        {
            var validation = await _paginationValidator.ValidateAsync(p);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(p.Search))
                query = query.Where(u =>
                    u.FullName.Contains(p.Search.Trim()) ||
                    u.Email.Contains(p.Search.Trim()) ||
                    (u.StudentCode != null && u.StudentCode.Contains(p.Search.Trim())));

            var total = await query.CountAsync();

            var users = await query
                .OrderByDynamic(p.SortBy ?? "CreatedAt", p.SortOrder ?? "desc")
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .Select(u => new UserAdminDto(
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.StudentCode,
                    u.Role,
                    u.AccountStatus,
                    u.Avatar,
                    u.CreatedAt.Value.ConvertToVietnamTime(),
                    u.LastLogin.HasValue ? u.LastLogin.Value.ConvertToVietnamTime() : null
                ))
                .ToListAsync();

            return Ok(ApiResponse<List<UserAdminDto>>.SuccessResponse(users, "Lấy danh sách người dùng thành công", total));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            var dto = new UserAdminDto(
                user.UserId,
                user.FullName,
                user.Email,
                user.Phone,
                user.StudentCode,
                user.Role,
                user.AccountStatus,
                user.Avatar,
                user.CreatedAt.Value.ConvertToVietnamTime(),
                user.LastLogin.HasValue ? user.LastLogin.Value.ConvertToVietnamTime() : null
            );

            return Ok(ApiResponse<UserAdminDto>.SuccessResponse(dto));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAdminUserDto dto)
        {
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email.Trim()))
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
            var validation = await _updateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(ApiResponse<object>.FailResponse("Không tìm thấy người dùng"));

            if (user.UserId == CurrentUserId)
                return BadRequest(ApiResponse<object>.FailResponse("Không thể cập nhật thông tin chính mình qua API này"));

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email.Trim() && u.UserId != id))
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
            var validation = await _roleValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

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
            var validation = await _statusValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validation.Errors.First().ErrorMessage));

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