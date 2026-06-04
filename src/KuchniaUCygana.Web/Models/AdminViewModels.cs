using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.Admin;

namespace KuchniaUCygana.Web.Models;

public sealed class AdminDashboardViewModel
{
    public IReadOnlyList<SystemLogDto> SystemLogs { get; init; } = [];

    public IReadOnlyList<UserDto> Users { get; init; } = [];
}
