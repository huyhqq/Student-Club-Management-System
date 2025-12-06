namespace ClubManagementApi.DTO
{
    public record RegisterDto(string FullName, string Email, string Password, string? Phone, string? Role);
    public record VerifyOtpDto(string Email, string Otp);
    public record ResendOtpDto(string Email);
    public record LoginDto(string Email, string Password);
    public record ForgotPasswordDto(string Email);
    public record ResetPasswordWithOtpDto(string Email, string Otp, string NewPassword);
}