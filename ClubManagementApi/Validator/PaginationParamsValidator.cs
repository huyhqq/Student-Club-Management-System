using ClubManagementApi.Params;
using FluentValidation;

namespace ClubManagementApi.Validator
{
    public class PaginationParamsValidator : AbstractValidator<PaginationParams>
    {
        public PaginationParamsValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1).WithMessage("Số trang phải ≥ 1");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100).WithMessage("Kích thước trang từ 1 đến 100");

            RuleFor(x => x.Search)
                .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Search))
                .WithMessage("Từ khóa tìm kiếm không được quá 100 ký tự");
        }
    }
}