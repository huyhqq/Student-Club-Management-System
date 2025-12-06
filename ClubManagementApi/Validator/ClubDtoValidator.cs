using ClubManagementApi.Controllers;
using FluentValidation;

namespace ClubManagementApi.Validator
{
    public class CreateClubDtoValidator : AbstractValidator<ClubsController.CreateClubDto>
    {
        public CreateClubDtoValidator()
        {
            RuleFor(x => x.ClubName)
                .NotEmpty().WithMessage("Tên CLB không được để trống")
                .Length(5, 100).WithMessage("Tên CLB từ 5-100 ký tự");
            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Mô tả không quá 2000 ký tự");
            RuleFor(x => x.JoinFee)
                .GreaterThanOrEqualTo(0).When(x => x.JoinFee.HasValue).WithMessage("Phí tham gia phải lớn hơn hoặc bằng 0");
        }
    }
    public class UpdateClubDtoValidator : AbstractValidator<ClubsController.UpdateClubDto>
    {
        public UpdateClubDtoValidator()
        {
            RuleFor(x => x.ClubName)
                .NotEmpty().WithMessage("Tên CLB không được để trống")
                .Length(5, 100).WithMessage("Tên CLB từ 5-100 ký tự");
            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Mô tả không quá 2000 ký tự");
            RuleFor(x => x.JoinFee)
                .GreaterThanOrEqualTo(0).When(x => x.JoinFee.HasValue).WithMessage("Phí tham gia phải lớn hơn hoặc bằng 0");
        }
    }
}