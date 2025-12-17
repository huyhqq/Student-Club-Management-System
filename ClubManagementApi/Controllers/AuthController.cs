using ClubManagementApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using ClubManagementApi.Services;

namespace ClubManagementApi.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly StudentClubContext _context;
        private readonly IConfiguration _config;

        public AuthController(StudentClubContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }


        private string GenerateOtp() => new Random().Next(100000, 999999).ToString();

        private static string GenerateActivationToken(int length = 50)
        {
            var bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            var token = Convert.ToBase64String(bytes)
                .Replace("+", "").Replace("/", "").Replace("=", "");
            return token.Length > length ? token[..length] : token;
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.UserId.ToString()),
                new Claim("FullName", user.FullName),
                new Claim("Role", user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private IActionResult ValidationErrorResponse()
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new { Field = x.Key, Message = x.Value?.Errors.First().ErrorMessage })
                .ToList();

            return BadRequest(ApiResponse<object>.FailResponse("Dữ liệu không hợp lệ", errors));
        }


        public class RegisterDto
        {
            [Required(ErrorMessage = "Họ và tên là bắt buộc")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 100 ký tự")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
            [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
            public string Password { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
            public string? Phone { get; set; }

            [RegularExpression("^(Student|ClubLeader|Admin)$", ErrorMessage = "Vai trò chỉ có thể là Student, ClubLeader hoặc Admin")]
            public string? Role { get; set; }
        }

        public class VerifyOtpDto
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "OTP là bắt buộc")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP phải là 6 chữ số")]
            public string Otp { get; set; } = string.Empty;
        }

        public class ResendOtpDto
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; } = string.Empty;
        }

        public class LoginDto
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
            public string Password { get; set; } = string.Empty;
        }

        public class ForgotPasswordDto
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            public string Email { get; set; } = string.Empty;
        }

        public class ResetPasswordWithOtpDto
        {
            [Required(ErrorMessage = "Email là bắt buộc")]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "OTP là bắt buộc")]
            [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP phải là 6 chữ số")]
            public string Otp { get; set; } = string.Empty;

            [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
            [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự")]
            public string NewPassword { get; set; } = string.Empty;
        }

        // ==================== CÁC ENDPOINT ====================

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(ApiResponse<object>.FailResponse("Email đã tồn tại"));

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = dto.Email.Trim(),
                Phone = dto.Phone?.Trim(),
                Avatar = null,
                PasswordHash = Encoding.UTF8.GetBytes(passwordHash),
                Role = string.IsNullOrWhiteSpace(dto.Role) ? "Student" : dto.Role.Trim(),
                AccountStatus = "PendingVerification",
                CreatedAt = TimeZoneHelper.NowInVietnam
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Xóa token kích hoạt cũ
            var oldTokens = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "Activation");
            _context.UserTokens.RemoveRange(oldTokens);

            var token = GenerateActivationToken(50);
            var expiryUtc = DateTime.UtcNow.AddMinutes(15);
            var expiryVn = expiryUtc.ConvertToVietnamTime();

            _context.UserTokens.Add(new UserToken
            {
                UserId = user.UserId,
                Token = token,
                TokenType = "Activation",
                ExpiryDate = expiryUtc,
                CreatedAt = TimeZoneHelper.NowInVietnam
            });
            await _context.SaveChangesAsync();

            var activationLink = $"{_config["AppSettings:LinkActivation"]}?email={user.Email}&token={token}";

            FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendActivationEmailAsync(user.FullName, user.Email, activationLink, expiryVn);
            });

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.UserId,
                Message = "Đăng ký thành công. Vui lòng kiểm tra email để kích hoạt tài khoản.",
                ExpiresAt = expiryVn
            }));
        }

        [HttpGet("activate")]
        public async Task<IActionResult> ActivateAccount([FromQuery] string email, [FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return BadRequest(ApiResponse<object>.FailResponse("Thiếu thông tin kích hoạt"));

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.AccountStatus == "PendingVerification");

            if (user == null)
                return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc đã được kích hoạt"));

            var tokenCheck = await _context.UserTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.Token == token &&
                    t.TokenType == "Activation" &&
                    t.ExpiryDate > DateTime.UtcNow &&
                    t.IsUsed == false);

            if (tokenCheck == null)
                return BadRequest(ApiResponse<object>.FailResponse("Mã kích hoạt không hợp lệ hoặc đã hết hạn"));

            user.AccountStatus = "Active";
            tokenCheck.IsUsed = true;
            await _context.SaveChangesAsync();

            var jwt = GenerateJwtToken(user);

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Token = jwt,
                user.UserId,
                user.FullName,
                user.Role,
                Message = "Kích hoạt tài khoản thành công! Bạn có thể đăng nhập ngay."
            }));
        }

        [HttpPost("resend-activation")]
        public async Task<IActionResult> ResendActivation([FromBody] ResendOtpDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.AccountStatus == "PendingVerification");

            if (user == null)
                return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc đã được kích hoạt"));

            var oldTokens = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "Activation");
            _context.UserTokens.RemoveRange(oldTokens);

            var token = GenerateActivationToken(50);
            var expiryUtc = DateTime.UtcNow.AddMinutes(15);
            var expiryVn = expiryUtc.ConvertToVietnamTime();

            _context.UserTokens.Add(new UserToken
            {
                UserId = user.UserId,
                Token = token,
                TokenType = "Activation",
                ExpiryDate = expiryUtc,
                CreatedAt = TimeZoneHelper.NowInVietnam
            });
            await _context.SaveChangesAsync();

            var link = $"{_config["AppSettings:LinkActivation"]}?email={user.Email}&token={token}";

            FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendActivationEmailAsync(user.FullName, user.Email, link, expiryVn);
            });

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Message = "Đã gửi lại mã kích hoạt",
                ExpiresAt = expiryVn
            }));
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.AccountStatus == "Active");
            if (user == null) return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc chưa kích hoạt"));

            var tokenRecord = await _context.UserTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.Token == dto.Otp &&
                    t.TokenType == "ResetPassword" &&
                    t.IsUsed == false &&
                    t.ExpiryDate > DateTime.UtcNow);

            if (tokenRecord == null)
                return BadRequest(ApiResponse<object>.FailResponse("OTP không đúng hoặc đã hết hạn"));

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.UserId,
                user.FullName,
                user.Role,
                Message = "Xác minh OTP thành công"
            }));
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.AccountStatus == "Active");
            if (user == null) return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc chưa kích hoạt"));

            var oldTokens = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "ResetPassword");
            _context.UserTokens.RemoveRange(oldTokens);

            var otp = GenerateOtp();
            var expiryUtc = DateTime.UtcNow.AddMinutes(10);
            var expiryVn = expiryUtc.ConvertToVietnamTime();

            _context.UserTokens.Add(new UserToken
            {
                UserId = user.UserId,
                Token = otp,
                TokenType = "ResetPassword",
                ExpiryDate = expiryUtc,
                CreatedAt = TimeZoneHelper.NowInVietnam
            });
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendOtpEmailAsync(user.FullName, user.Email, otp, expiryVn);
            });

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Message = "Đã gửi lại OTP",
                ExpiresAt = expiryVn
            }));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || user.AccountStatus != "Active")
                return Unauthorized(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc chưa được kích hoạt"));

            var storedHash = Encoding.UTF8.GetString(user.PasswordHash);
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, storedHash))
                return Unauthorized(ApiResponse<object>.FailResponse("Mật khẩu không đúng"));

            user.LastLogin = TimeZoneHelper.NowInVietnam;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Token = GenerateJwtToken(user),
                user.UserId,
                user.FullName,
                user.Email,
                user.Role,
                user.Avatar,
                user.Phone
            }));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return Ok(ApiResponse<object>.SuccessResponse(null, "Nếu email tồn tại, OTP sẽ được gửi đến email"));

            var oldTokens = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "ResetPassword");
            _context.UserTokens.RemoveRange(oldTokens);

            var otp = GenerateOtp();
            var expiryUtc = DateTime.UtcNow.AddMinutes(10);
            var expiryVn = expiryUtc.ConvertToVietnamTime();

            _context.UserTokens.Add(new UserToken
            {
                UserId = user.UserId,
                Token = otp,
                TokenType = "ResetPassword",
                ExpiryDate = expiryUtc,
                CreatedAt = TimeZoneHelper.NowInVietnam
            });
            await _context.SaveChangesAsync();

            FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendOtpEmailAsync(user.FullName, user.Email, otp, expiryVn);
            });

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Message = "OTP đặt lại mật khẩu đã được gửi đến email",
                ExpiresAt = expiryVn.ToString("dd/MM/yyyy HH:mm:ss")
            }));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithOtpDto dto)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại"));

            var tokenRecord = await _context.UserTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.Token == dto.Otp &&
                    t.TokenType == "ResetPassword" &&
                    t.IsUsed == false &&
                    t.ExpiryDate > DateTime.UtcNow);

            if (tokenRecord == null)
                return BadRequest(ApiResponse<object>.FailResponse("OTP không đúng hoặc đã hết hạn"));

            var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.PasswordHash = Encoding.UTF8.GetBytes(newHash);
            tokenRecord.IsUsed = true;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(null, "Đặt lại mật khẩu thành công"));
        }

        private void FireAndForget(Func<Task> taskFunc)
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