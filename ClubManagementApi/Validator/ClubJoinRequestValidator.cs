using ClubManagementApi.Controllers;
using FluentValidation;

namespace ClubManagementApi.Validator
{
    public class CreateJoinRequestDtoValidator : AbstractValidator<ClubJoinRequestsController.CreateJoinRequestDto>
    {
        public CreateJoinRequestDtoValidator()
        {
            RuleFor(x => x.StudentId)
             .NotEmpty().WithMessage("Mã số sinh viên là bắt buộc")
             .Matches(@"^SE\d{6}$").WithMessage("Mã số sinh viên phải bắt đầu bằng 'SE' và theo sau là 6 chữ số");


            RuleFor(x => x.Major)
                .NotEmpty().WithMessage("Ngành học không được để trống")
                .MaximumLength(200).WithMessage("Ngành học không quá 200 ký tự");

            RuleFor(x => x.AcademicYear)
                .NotEmpty().WithMessage("Năm học không được để trống")
                .Matches(@"^\d{4}-\d{4}$").WithMessage("Năm học phải có định dạng YYYY-YYYY (ví dụ: 2023-2024)");

            RuleFor(x => x.Introduction)
                .NotEmpty().WithMessage("Giới thiệu bản thân không được để trống")
                .MinimumLength(20).WithMessage("Giới thiệu bản thân ít nhất 20 ký tự")
                .MaximumLength(1000).WithMessage("Giới thiệu bản thân không quá 1000 ký tự");

            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Lý do tham gia không được để trống")
                .MinimumLength(20).WithMessage("Lý do tham gia ít nhất 20 ký tự")
                .MaximumLength(1000).WithMessage("Lý do tham gia không quá 1000 ký tự");

            RuleFor(x => x.ContactInfoOptional)
                .MaximumLength(200).When(x => x.ContactInfoOptional != null)
                .WithMessage("Thông tin liên hệ không quá 200 ký tự");
        }
    }
}