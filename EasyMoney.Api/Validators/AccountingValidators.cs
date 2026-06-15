using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using FluentValidation;

namespace EasyMoney.Api.Validators;

public class JournalLineInputValidator : AbstractValidator<JournalLineInput>
{
    public JournalLineInputValidator()
    {
        RuleFor(x => x.Debit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Credit).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(l => (l.Debit > 0 && l.Credit == 0) || (l.Debit == 0 && l.Credit > 0))
            .WithMessage("Each line must have exactly one of debit or credit > 0");
        When(x => x.Target == EntryTarget.GL, () =>
        {
            RuleFor(x => x.GlAccountCode).NotEmpty().WithMessage("GL line requires glAccountCode");
            RuleFor(x => x.MemberAccountId).Null().WithMessage("GL line must not have memberAccountId");
        });
        When(x => x.Target == EntryTarget.MEMBER_ACCOUNT, () =>
        {
            RuleFor(x => x.MemberAccountId).NotNull().WithMessage("MEMBER_ACCOUNT line requires memberAccountId");
            RuleFor(x => x.GlAccountCode).Empty().WithMessage("MEMBER_ACCOUNT line must not have glAccountCode");
        });
    }
}

public class PostJournalRequestValidator : AbstractValidator<PostJournalRequest>
{
    public PostJournalRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count >= 2)
            .WithMessage("Journal must have at least 2 lines");
        RuleForEach(x => x.Lines).SetValidator(new JournalLineInputValidator());
        RuleFor(x => x).Must(r =>
        {
            var d = r.Lines.Sum(l => l.Debit);
            var c = r.Lines.Sum(l => l.Credit);
            return Math.Round(d, 2) == Math.Round(c, 2);
        }).WithMessage("Sum(debit) must equal Sum(credit)");
    }
}
