using AutoMapper;
using KuchniaUCygana.Application.DTOs.Admin;
using KuchniaUCygana.Application.DTOs.CustomerService;
using KuchniaUCygana.Application.DTOs.HR;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Entities.HR;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Application.Mappings;

public sealed class Module5Profile : Profile
{
    public Module5Profile()
    {
        CreateMap<Department, DepartmentDto>()
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description ?? string.Empty))
            .ForMember(dest => dest.HeadEmployeeFullName, opt => opt.Ignore());
        CreateMap<CreateDepartmentRequest, Department>()
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description ?? string.Empty));
        CreateMap<UpdateDepartmentRequest, Department>()
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description ?? string.Empty));

        CreateMap<Employee, EmployeeDto>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
            .ForMember(dest => dest.DepartmentName, opt => opt.Ignore());
        CreateMap<CreateEmployeeRequest, Employee>();
        CreateMap<UpdateEmployeeRequest, Employee>()
            .ForMember(dest => dest.UserId, opt => opt.Ignore());

        CreateMap<LeaveRequest, LeaveRequestDto>()
            .ForMember(dest => dest.EmployeeFullName, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedByEmployeeFullName, opt => opt.Ignore());
        CreateMap<CreateLeaveRequestRequest, LeaveRequest>();
        CreateMap<UpdateLeaveRequestRequest, LeaveRequest>()
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedByEmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.RejectionReason, opt => opt.Ignore());
        CreateMap<ReviewLeaveRequestRequest, LeaveRequest>()
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.LeaveType, opt => opt.Ignore())
            .ForMember(dest => dest.StartDate, opt => opt.Ignore())
            .ForMember(dest => dest.EndDate, opt => opt.Ignore());

        CreateMap<WorkSchedule, WorkScheduleDto>()
            .ForMember(dest => dest.Shift, opt => opt.MapFrom(src => src.Shift.ToString()))
            .ForMember(dest => dest.EmployeeFullName, opt => opt.Ignore());
        CreateMap<CreateWorkScheduleRequest, WorkSchedule>();
        CreateMap<UpdateWorkScheduleRequest, WorkSchedule>();

        CreateMap<Ticket, TicketDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.Priority, opt => opt.MapFrom(src => src.Priority.ToString()))
            .ForMember(dest => dest.ClientFullName, opt => opt.Ignore())
            .ForMember(dest => dest.AssignedToFullName, opt => opt.Ignore());
        CreateMap<CreateTicketRequest, Ticket>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => TicketStatus.New))
            .ForMember(dest => dest.AssignedToUserId, opt => opt.Ignore())
            .ForMember(dest => dest.ClosedAt, opt => opt.Ignore());
        CreateMap<UpdateTicketRequest, Ticket>()
            .ForMember(dest => dest.ClientUserId, opt => opt.Ignore())
            .ForMember(dest => dest.ClosedAt, opt => opt.Ignore());
        CreateMap<AssignTicketRequest, Ticket>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.TicketId));
        CreateMap<ChangeTicketStatusRequest, Ticket>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.TicketId))
            .ForMember(dest => dest.ClosedAt, opt => opt.MapFrom(src =>
                src.Status == TicketStatus.Closed || src.Status == TicketStatus.Resolved
                    ? DateTimeOffset.UtcNow
                    : (DateTimeOffset?)null));

        CreateMap<TicketAttachment, TicketAttachmentDto>()
            .ForMember(dest => dest.UploadedByFullName, opt => opt.Ignore());
        CreateMap<CreateTicketAttachmentRequest, TicketAttachment>()
            .ForMember(dest => dest.UploadedAt, opt => opt.MapFrom(_ => DateTimeOffset.UtcNow));

        CreateMap<SystemLog, SystemLogDto>()
            .ForMember(dest => dest.UserFullName, opt => opt.Ignore());
        CreateMap<CreateSystemLogRequest, SystemLog>()
            .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(_ => DateTimeOffset.UtcNow));
    }
}
