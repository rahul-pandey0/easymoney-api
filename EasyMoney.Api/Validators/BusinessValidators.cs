using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using FluentValidation;

namespace EasyMoney.Api.Validators;

public class OpenAccountRequestValidator : AbstractValidator<OpenAccountRequest>
{
    private readonly ITenantContext _ctx;

    public OpenAccountRequestValidator( ITenantContext ctx)
    {
         _ctx = ctx;
    }

    public OpenAccountRequestValidator()
    {
        RuleFor(x => x.MonthlyContribution).GreaterThan(0);
        RuleFor(x => x.AccountOpenDate).Must(d => d <= _ctx.CurrentDate)
            .WithMessage("Account open date cannot be in the future");
    }
}

public class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    private readonly ITenantContext _ctx;

    public RecordPaymentRequestValidator(ITenantContext ctx) 
    {
        _ctx = ctx;
    }
    public RecordPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        //RuleFor(x => x.PaidDate).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
        RuleFor(x => x.PaidDate).Must(d => d <= _ctx.CurrentDate)

    .WithMessage("Paid date cannot be in the future");
        RuleFor(x => x.Method).NotEmpty();
    }
}

//public class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
//{
//    public RecordPaymentRequestValidator()
//    {
//        RuleFor(x => x.InstallmentAmount)
//            .GreaterThan(0);

//        RuleFor(x => x.PenaltyAmount)
//            .GreaterThanOrEqualTo(0);

//        RuleFor(x => x.OtherCharges)
//            .GreaterThanOrEqualTo(0);

//        RuleFor(x => x.TotalAmount)
//            .GreaterThan(0);

//        RuleFor(x => x.PaidDate)
//            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
//            .WithMessage("Paid date cannot be in the future");

//        RuleFor(x => x.Method)
//            .NotEmpty();

//        RuleFor(x => x.VoucherNo)
//            .MaximumLength(100);

//        RuleFor(x => x.Remarks)
//            .MaximumLength(500);
//    }
//}

public class SubmitBidRequestValidator : AbstractValidator<SubmitBidRequest>
{
    public SubmitBidRequestValidator() => RuleFor(x => x.BidPct).InclusiveBetween(0, 100);
}

