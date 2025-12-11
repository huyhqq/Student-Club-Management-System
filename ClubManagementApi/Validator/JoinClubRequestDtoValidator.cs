using ClubManagementApi.Controllers;
using FluentValidation;

namespace ClubManagementApi.Validator
{
    public class JoinClubRequestDtoValidator : AbstractValidator<ClubMembersController.JoinClubRequestDto>
    {
        public JoinClubRequestDtoValidator()
        {
            RuleFor(x => x.ClubId)
                .GreaterThan(0).WithMessage("CLB không hợp lệ");

            RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã số sinh viên là bắt buộc")
            .Matches(@"^SE\d{6}$").WithMessage("Mã số sinh viên phải bắt đầu bằng 'SE' và theo sau là 6 chữ số");


            RuleFor(x => x.Major)
                .NotEmpty().WithMessage("Ngành học không được để trống")
                .MaximumLength(100);

            RuleFor(x => x.AcademicYear)
                .NotEmpty().WithMessage("Năm học không được để trống")
                .Matches(@"^\d{4}-\d{4}$").WithMessage("Năm học phải định dạng 2023-2024");

            RuleFor(x => x.Introduction)
                .NotEmpty().WithMessage("Giới thiệu bản thân là bắt buộc")
                .MinimumLength(50).WithMessage("Giới thiệu ít nhất 50 ký tự");

            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Lý do tham gia là bắt buộc")
                .MinimumLength(50).WithMessage("Lý do tham gia ít nhất 50 ký tự");
        }
    }
}