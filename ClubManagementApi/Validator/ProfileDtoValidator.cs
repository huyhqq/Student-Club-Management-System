using ClubManagementApi.Controllers;
using FluentValidation;

namespace ClubManagementApi.Validator
{
    public class UpdateProfileDtoValidator : AbstractValidator<ProfileController.UpdateProfileDto>
    {
        public UpdateProfileDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống")
                .Length(2, 100).WithMessage("Họ tên từ 2-100 ký tự");

            RuleFor(x => x.Phone)
                .Matches(@"^0[3|5|7|8|9]\d{8}$")
                .When(x => !string.IsNullOrEmpty(x.Phone))
                .WithMessage("Số điện thoại không hợp lệ (VD: 0901234567)");
        }
    }

    public class ChangePasswordDtoValidator : AbstractValidator<ProfileController.ChangePasswordDto>
    {
        public ChangePasswordDtoValidator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại");

            RuleFor(x => x.NewPassword)
                .NotEmpty().MinimumLength(6).WithMessage("Mật khẩu mới phải ít nhất 6 ký tự");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu không khớp");
        }
    }
}