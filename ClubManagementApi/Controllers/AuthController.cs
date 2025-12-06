using ClubManagementApi.DTO;
using ClubManagementApi.Models;
using ClubManagementApi.Helpers;
using ClubManagementApi.Services;
using ClubManagementApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using ClubManagementApi.Validator;

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

        public static string GenerateActivationToken(int length = 32)
        {
            if (length <= 0) throw new ArgumentException("Độ dài phải lớn hơn 0", nameof(length));

            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            string token = Convert.ToBase64String(bytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "");

            return token.Length > length ? token.Substring(0, length) : token;
        }
        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.UserId.ToString()),
                new Claim("FullName", user.FullName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var validationResult = await new RegisterDtoValidator().ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(validationResult.Errors.First().ErrorMessage));

            if (_context.Users.Any(u => u.Email == dto.Email))
                return BadRequest(ApiResponse<object>.FailResponse("Email đã tồn tại"));

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                Avatar = null,
                PasswordHash = Encoding.UTF8.GetBytes(passwordHash),
                Role = dto.Role ?? "Student",
                AccountStatus = "PendingVerification",
                CreatedAt = TimeZoneHelper.NowInVietnam
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var existingOtp = await _context.UserTokens
                .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.TokenType == "Activation" && t.ExpiryDate > DateTime.UtcNow);

            string token;
            DateTime vietnamExpiry;

            if (existingOtp != null)
            {
                token = existingOtp.Token;
                vietnamExpiry = existingOtp.ExpiryDate.ConvertToVietnamTime();
            }
            else
            {
                var oldOtps = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "Activation");
                _context.UserTokens.RemoveRange(oldOtps);

                token = GenerateActivationToken(50);
                var expiryUtc = DateTime.UtcNow.AddMinutes(10);
                vietnamExpiry = expiryUtc.ConvertToVietnamTime();

                _context.UserTokens.Add(new UserToken
                {
                    UserId = user.UserId,
                    Token = token,
                    TokenType = "Activation",
                    ExpiryDate = expiryUtc,
                    CreatedAt = TimeZoneHelper.NowInVietnam
                });
                await _context.SaveChangesAsync();
            }

            var activationLink = $"{_config["AppSettings:LinkActivation"]}?email={user.Email}&token={token}";

            TaskHelper.FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendActivationEmailAsync(
                    user.FullName,
                    user.Email,
                    activationLink,
                    vietnamExpiry
                );
            });

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.UserId,
                Message = "Đã gửi OTP đến email của bạn",
                ExpiresAt = vietnamExpiry
            }));
        }

        [HttpGet("activate")]
        public async Task<IActionResult> ActivateAccount([FromQuery] string email, [FromQuery] string token)
        {
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

            if (token == null)
                return BadRequest(ApiResponse<object>.FailResponse("Mã OTP không hợp lệ hoặc đã hết hạn"));

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
            var validator = new ResendOtpDtoValidator();
            var result = await validator.ValidateAsync(dto);
            if (!result.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(result.Errors.First().ErrorMessage));

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.AccountStatus == "PendingVerification");

            if (user == null)
                return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc đã kích hoạt"));

            var oldTokens = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "Activation");
            _context.UserTokens.RemoveRange(oldTokens);

            var token = GenerateActivationToken(50);
            var expiry = DateTime.UtcNow.AddMinutes(15);

            _context.UserTokens.Add(new UserToken
            {
                UserId = user.UserId,
                Token = token,
                TokenType = "Activation",
                ExpiryDate = expiry,
                CreatedAt = TimeZoneHelper.NowInVietnam
            });

            await _context.SaveChangesAsync();

            var link = $"{_config["AppSettings:LinkActivation"]}?email={user.Email}&token={token}";

            TaskHelper.FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendActivationEmailAsync(user.FullName, user.Email, link, expiry.ConvertToVietnamTime());
            });

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Message = "Đã gửi lại mã kích hoạt",
                ExpiresAt = expiry.ConvertToVietnamTime()
            }));
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.AccountStatus == "Active");
            if (user == null) return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc chưa kích hoạt"));

            var token = await _context.UserTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.Token == dto.Otp &&
                    t.TokenType == "ResetPassword" &&
                    t.IsUsed == false &&
                    t.ExpiryDate > DateTime.UtcNow);

            if (token == null) return BadRequest(ApiResponse<object>.FailResponse("OTP không đúng hoặc đã hết hạn"));

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.UserId,
                user.FullName,
                user.Role,
                Message = "Xác minh thành công"
            }));
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.AccountStatus == "Active");
            if (user == null) return BadRequest(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc chưa kích hoạt"));

            var existing = await _context.UserTokens
                .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.TokenType == "ResetPassword" && t.ExpiryDate > DateTime.UtcNow && t.IsUsed == false);

            string otp;
            DateTime vietnamExpiry;

            if (existing != null)
            {
                otp = existing.Token;
                vietnamExpiry = existing.ExpiryDate.ConvertToVietnamTime();
            }
            else
            {
                var old = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "ResetPassword");
                _context.UserTokens.RemoveRange(old);

                otp = GenerateOtp();
                var expiryUtc = DateTime.UtcNow.AddMinutes(10);
                vietnamExpiry = expiryUtc.ConvertToVietnamTime();

                _context.UserTokens.Add(new UserToken
                {
                    UserId = user.UserId,
                    Token = otp,
                    TokenType = "ResetPassword",
                    ExpiryDate = expiryUtc,
                    CreatedAt = TimeZoneHelper.NowInVietnam
                });
                await _context.SaveChangesAsync();
            }

            TaskHelper.FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendOtpEmailAsync(user.FullName, user.Email, otp, vietnamExpiry);

            });

            return Ok(ApiResponse<object>.SuccessResponse(new { ExpiresAt = vietnamExpiry }, "Đã gửi lại OTP"));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var validator = new LoginDtoValidator();
            var result = await validator.ValidateAsync(dto);
            if (!result.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(result.Errors.First().ErrorMessage));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || user.AccountStatus != "Active")
                return Unauthorized(ApiResponse<object>.FailResponse("Tài khoản không tồn tại hoặc chưa kích hoạt"));

            var storedHash = Encoding.UTF8.GetString(user.PasswordHash);
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, storedHash))
                return Unauthorized(ApiResponse<object>.FailResponse("Sai mật khẩu"));

            user.LastLogin = TimeZoneHelper.NowInVietnam;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Token = GenerateJwtToken(user),
                user.UserId,
                user.FullName,
                user.Email,
                user.Role,
                user.Avatar
            }));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var validator = new EmailOnlyDtoValidator();
            var result = await validator.ValidateAsync(dto);
            if (!result.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(result.Errors.First().ErrorMessage));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return Ok(ApiResponse<string>.SuccessResponse(null, "Nếu email tồn tại, OTP sẽ được gửi"));

            var existingToken = await _context.UserTokens
                .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.TokenType == "ResetPassword" && t.ExpiryDate > DateTime.UtcNow);

            string otp;
            DateTime vietnamExpiry;

            if (existingToken != null)
            {
                otp = existingToken.Token;
                vietnamExpiry = existingToken.ExpiryDate.ConvertToVietnamTime();
            }
            else
            {
                var oldTokens = _context.UserTokens.Where(t => t.UserId == user.UserId && t.TokenType == "ResetPassword");
                _context.UserTokens.RemoveRange(oldTokens);

                otp = GenerateOtp();
                var expiryUtc = DateTime.UtcNow.AddMinutes(10);
                vietnamExpiry = expiryUtc.ConvertToVietnamTime();

                _context.UserTokens.Add(new UserToken
                {
                    UserId = user.UserId,
                    Token = otp,
                    TokenType = "ResetPassword",
                    ExpiryDate = expiryUtc,
                    CreatedAt = TimeZoneHelper.NowInVietnam
                });
                await _context.SaveChangesAsync();
            }

            TaskHelper.FireAndForget(async () =>
            {
                var emailService = new EmailService();
                await emailService.SendOtpEmailAsync(
                    user.FullName,
                    user.Email,
                    otp,
                    vietnamExpiry
                );
            });

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Message = "OTP đặt lại mật khẩu đã được gửi đến email",
                ExpiresAt = vietnamExpiry.ToString("dd/MM/yyyy HH:mm:ss")
            }));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithOtpDto dto)
        {
            var validator = new ResetPasswordWithOtpDtoValidator();
            var result = await validator.ValidateAsync(dto);
            if (!result.IsValid)
                return BadRequest(ApiResponse<object>.FailResponse(result.Errors.First().ErrorMessage));

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

            return Ok(ApiResponse<string>.SuccessResponse(null, "Đặt lại mật khẩu thành công"));
        }
    }
}