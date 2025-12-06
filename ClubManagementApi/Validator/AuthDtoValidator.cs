using FluentValidation;
using ClubManagementApi.DTO;

namespace ClubManagementApi.Validator
{
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        public RegisterDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty()
                .MaximumLength(100)
                .WithMessage("Họ tên không được để trống");

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Email không hợp lệ");

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(6)
                .WithMessage("Mật khẩu phải ít nhất 6 ký tự");

            RuleFor(x => x.Phone)
                .Matches(@"^0[3|5|7|8|9]\d{8}$")
                .When(x => !string.IsNullOrEmpty(x.Phone))
                .WithMessage("Số điện thoại không hợp lệ");

            RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => new[] { "Student", "ClubLeader"}.Contains(role))
            .WithMessage("Role phải là 'Student', 'ClubLeader'");
        }
    }

    public class LoginDtoValidator : AbstractValidator<LoginDto>
    {
        public LoginDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class ResetPasswordWithOtpDtoValidator : AbstractValidator<ResetPasswordWithOtpDto>
    {
        public ResetPasswordWithOtpDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Otp).NotEmpty().Length(6);
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
        }
    }

    public class EmailOnlyDtoValidator : AbstractValidator<ForgotPasswordDto>
    {
        public EmailOnlyDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public class ResendOtpDtoValidator : AbstractValidator<ResendOtpDto>
    {
        public ResendOtpDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }
}
