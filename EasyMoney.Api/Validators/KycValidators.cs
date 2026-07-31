using EasyMoney.Api.Dtos;
using FluentValidation;

namespace EasyMoney.Api.Validators;

public class CreateMemberRequestValidator : AbstractValidator<CreateMemberRequest>
{
    public CreateMemberRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^[0-9]{10}$")
            .WithMessage("Phone must be a 10-digit number");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Pincode).Matches(@"^[0-9]{6}$")
            .When(x => !string.IsNullOrEmpty(x.Pincode))
            .WithMessage("Pincode must be 6 digits");
        RuleFor(x => x.BankIfsc).Matches(@"^[A-Z]{4}0[A-Z0-9]{6}$")
            .When(x => !string.IsNullOrEmpty(x.BankIfsc))
            .WithMessage("Invalid IFSC format");
    }
}

public class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberRequestValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.FullName));
        RuleFor(x => x.Phone).Matches(@"^[0-9]{10}$").When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Pincode).Matches(@"^[0-9]{6}$").When(x => !string.IsNullOrEmpty(x.Pincode));
        RuleFor(x => x.BankIfsc).Matches(@"^[A-Z]{4}0[A-Z0-9]{6}$").When(x => !string.IsNullOrEmpty(x.BankIfsc));
    }
}

public class UpsertIndividualKycRequestValidator : AbstractValidator<UpsertIndividualKycRequest>
{
    public UpsertIndividualKycRequestValidator()
    {
        RuleFor(x => x.PanNumber).Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
            .When(x => !string.IsNullOrEmpty(x.PanNumber))
            .WithMessage("Invalid PAN format (expected AAAAA9999A)");
        RuleFor(x => x.AadhaarNumber).Matches(@"^[0-9]{12}$")
            .When(x => !string.IsNullOrEmpty(x.AadhaarNumber))
            .WithMessage("Aadhaar must be 12 digits");
        RuleFor(x => x.AadhaarLast4).Matches(@"^[0-9]{4}$")
            .When(x => !string.IsNullOrEmpty(x.AadhaarLast4));
        RuleFor(x => x.PermanentPincode).Matches(@"^[0-9]{6}$")
            .When(x => !string.IsNullOrEmpty(x.PermanentPincode));
    }
}

public class UpsertCorporateKycRequestValidator : AbstractValidator<UpsertCorporateKycRequest>
{
    //public UpsertCorporateKycRequestValidator()
    //{
    //    RuleFor(x => x.PanNumber).Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
    //        .When(x => !string.IsNullOrEmpty(x.PanNumber));
    //    RuleFor(x => x.Gstin).Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9][A-Z][A-Z0-9]$")
    //        .When(x => !string.IsNullOrEmpty(x.Gstin))
    //        .WithMessage("Invalid GSTIN format");
    //    RuleFor(x => x.AuthorizedSignatoryPan).Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
    //        .When(x => !string.IsNullOrEmpty(x.AuthorizedSignatoryPan));
    //    RuleFor(x => x.AuthorizedSignatoryAadhaarLast4).Matches(@"^[0-9]{4}$")
    //        .When(x => !string.IsNullOrEmpty(x.AuthorizedSignatoryAadhaarLast4));
    //    RuleFor(x => x.RegisteredPincode).Matches(@"^[0-9]{6}$")
    //        .When(x => !string.IsNullOrEmpty(x.RegisteredPincode));
    //}
    public UpsertCorporateKycRequestValidator()
    {
        RuleFor(x => x.PanNumber)
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
            .When(x => !string.IsNullOrWhiteSpace(x.PanNumber));

        RuleFor(x => x.Gstin)
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9][A-Z][A-Z0-9]$")
            .When(x => !string.IsNullOrWhiteSpace(x.Gstin))
            .WithMessage("Invalid GSTIN format");

        // Registered Office
        When(x => x.RegisteredOffice != null, () =>
        {
            RuleFor(x => x.RegisteredOffice!.Pincode)
                .Matches(@"^[0-9]{6}$")
                .When(x => !string.IsNullOrWhiteSpace(x.RegisteredOffice!.Pincode));
        });

        // Contact Details
        RuleForEach(x => x.ContactDetails)
            .SetValidator(new CorporateContactDetailRequestValidator());

        // Directors
        RuleForEach(x => x.Directors)
            .SetValidator(new CorporateDirectorRequestValidator());

        //// Beneficial Owners
        //RuleForEach(x => x.BeneficialOwners)
        //    .SetValidator(new CorporateBeneficialOwnerRequestValidator());

        //// Authorized Signatories
        //RuleForEach(x => x.AuthorizedSignatories)
        //    .SetValidator(new CorporateAuthorizedSignatoryRequestValidator());
    }
    public class CorporateAuthorizedSignatoryRequestValidator
    : AbstractValidator<CorporateAuthorizedSignatoryRequest>
    {
        public CorporateAuthorizedSignatoryRequestValidator()
        {
            RuleFor(x => x.Pan)
                .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
                .When(x => !string.IsNullOrWhiteSpace(x.Pan));

            RuleFor(x => x.AadhaarLast4)
                .Matches(@"^[0-9]{4}$")
                .When(x => !string.IsNullOrWhiteSpace(x.AadhaarLast4));

            RuleFor(x => x.Email)
                .EmailAddress()
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        }
    }
    public class CorporateContactDetailRequestValidator
    : AbstractValidator<CorporateContactDetailRequest>
    {
        public CorporateContactDetailRequestValidator()
        {
            RuleFor(x => x.Email)
                .EmailAddress()
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        }
    }
    public class CorporateDirectorRequestValidator
    : AbstractValidator<CorporateDirectorRequest>
    {
        public CorporateDirectorRequestValidator()
        {
            RuleFor(x => x.Pan)
                .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
                .When(x => !string.IsNullOrWhiteSpace(x.Pan));
        }
    }
    public class CorporateBeneficialOwnerRequestValidator
    : AbstractValidator<CorporateBeneficialOwnerRequest>
    {
        public CorporateBeneficialOwnerRequestValidator()
        {
            RuleFor(x => x.Pan)
                .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$")
                .When(x => !string.IsNullOrWhiteSpace(x.Pan));
        }
    }
    public class RegisteredOfficeRequestValidator
    : AbstractValidator<RegisteredOfficeRequest>
    {
        public RegisteredOfficeRequestValidator()
        {
            RuleFor(x => x.Pincode)
                .Matches(@"^[0-9]{6}$")
                .When(x => !string.IsNullOrWhiteSpace(x.Pincode));
        }
    }
}

public class UploadKycDocumentRequestValidator : AbstractValidator<UploadKycDocumentRequest>
{
    public UploadKycDocumentRequestValidator()
    {
        // doc_type itself is the only required field; the file is multipart, validated in the controller.
    }
}

public class VerifyKycDocumentRequestValidator : AbstractValidator<VerifyKycDocumentRequest>
{
    public VerifyKycDocumentRequestValidator()
    {
        RuleFor(x => x.RejectionReason)
            .NotEmpty().When(x => !x.Verified)
            .WithMessage("Rejection reason is required when rejecting a document");
    }
}

public class KycReviewRequestValidator : AbstractValidator<KycReviewRequest>
{
    public KycReviewRequestValidator()
    {
        // Most transitions need remarks; REJECTED especially. Enforce at service layer for nuanced rules.
        RuleFor(x => x.Remarks).MaximumLength(255);
    }
}
