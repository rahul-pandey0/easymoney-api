using EasyMoney.Api.Domain;

namespace EasyMoney.Api.Dtos;

// ============================================================
// Department
// ============================================================

public record CreateDepartmentRequest(
    string DepartmentName,
    string? Description);

public record UpdateDepartmentRequest(
    string DepartmentName,
    string? Description,
    bool IsActive);

public record DepartmentDto(
    long DepartmentId,
    string DepartmentName,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);