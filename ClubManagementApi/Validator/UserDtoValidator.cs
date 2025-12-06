using ClubManagementApi.Controllers;
using FluentValidation;

namespace ClubManagementApi.Validator
{
    public class UpdateUserRoleDtoValidator : AbstractValidator<UsersController.UpdateUserRoleDto>
    {
        private readonly string[] ValidRoles = { "Student", "ClubLeader", "Admin" };

        public UpdateUserRoleDtoValidator()
        {
            RuleFor(x => x.Role)
                .NotEmpty()
                .Must(role => ValidRoles.Contains(role))
                .WithMessage("Vai trò chỉ có thể là: Student, ClubLeader hoặc Admin");
        }
    }

    public class UpdateUserStatusDtoValidator : AbstractValidator<UsersController.UpdateUserStatusDto>
    {
        private readonly string[] ValidStatuses = { "Active", "Locked", "Disabled" };

        public UpdateUserStatusDtoValidator()
        {
            RuleFor(x => x.AccountStatus)
                .NotEmpty()
                .Must(status => ValidStatuses.Contains(status))
                .WithMessage("Trạng thái chỉ có thể là: Active, Locked hoặc Disabled");
        }
    }

    public class CreateAdminUserDtoValidator : AbstractValidator<UsersController.CreateAdminUserDto>
    {
        public CreateAdminUserDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống")
                .MaximumLength(100).WithMessage("Họ tên không quá 100 ký tự");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống")
                .EmailAddress().WithMessage("Email không hợp lệ");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu không được để trống")
                .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự");

            RuleFor(x => x.Phone)
                .Matches(@"^0[3|5|7|8|9]\d{8}$")
                .When(x => !string.IsNullOrEmpty(x.Phone))
                .WithMessage("Số điện thoại phải bắt đầu bằng 03/05/07/08/09 và có đúng 10 chữ số");

            RuleFor(x => x.StudentCode)
                .NotEmpty()
                .When(x => x.Role == "Student")
                .WithMessage("Mã sinh viên là bắt buộc đối với vai trò Student")
                .Must(code => code != null && code.Trim().Length == 8)
                .WithMessage("Mã sinh viên phải đúng 8 ký tự")
                .Must(code => code != null && code.Trim().StartsWith("SE", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Mã sinh viên phải bắt đầu bằng SE (ví dụ: SE123456)")
                .Must(code => code != null && long.TryParse(code.Trim().Substring(2), out _))
                .WithMessage("6 ký tự cuối của mã sinh viên phải là số")
                .When(x => !string.IsNullOrEmpty(x.StudentCode));

            RuleFor(x => x.Role)
                .NotEmpty().WithMessage("Vai trò không được để trống")
                .Must(r => new[] { "Student", "ClubLeader", "Admin" }.Contains(r))
                .WithMessage("Vai trò chỉ có thể là: Student, ClubLeader, Admin");
        }
    }

    public class UpdateAdminUserDtoValidator : AbstractValidator<UsersController.UpdateAdminUserDto>
    {
        public UpdateAdminUserDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ tên không được để trống")
                .MaximumLength(100).WithMessage("Họ tên không quá 100 ký tự");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống")
                .EmailAddress().WithMessage("Email không hợp lệ");

            RuleFor(x => x.Phone)
                .Matches(@"^0[3|5|7|8|9]\d{8}$")
                .When(x => !string.IsNullOrEmpty(x.Phone))
                .WithMessage("Số điện thoại phải bắt đầu bằng 03/05/07/08/09 và có đúng 10 chữ số");

            RuleFor(x => x.StudentCode)
                .Must(code => string.IsNullOrEmpty(code) || code.Trim().Length == 8)
                .WithMessage("Mã sinh viên phải đúng 8 ký tự")
                .Must(code => string.IsNullOrEmpty(code) || code.Trim().StartsWith("SE", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Mã sinh viên phải bắt đầu bằng SE")
                .Must(code => string.IsNullOrEmpty(code) || long.TryParse(code.Trim().Substring(2), out _))
                .WithMessage("6 ký tự cuối của mã sinh viên phải là số")
                .When(x => !string.IsNullOrEmpty(x.StudentCode));

            RuleFor(x => x.Avatar)
                .MaximumLength(500)
                .When(x => x.Avatar != null)
                .WithMessage("Link avatar không quá 500 ký tự");
        }
    }
}
