using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Access;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Access.Contracts;
using Nexgate.Service.Identity.Contracts;

namespace Nexgate.Api.Rest.Controllers.Access;

/// <summary>Horarios de los sitios: franjas por día de la semana en la zona horaria del sitio.</summary>
[Tags("Schedules")]
[Route(Prefix)]
public sealed class SchedulesController(IScheduleService schedules, ICurrentPermissions permissions) : ApiControllerBase
{
    private readonly AccessScope scope = new(permissions);

    [HttpGet("sites/{siteId:guid}/schedules")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("listSchedules")]
    [EndpointSummary("Listar los horarios de un sitio")]
    [ProducesResponseType<List<ScheduleDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, CancellationToken ct) =>
        scope.CanRead(siteId) ? AccessResults.ToHttp(await schedules.ListAsync(siteId, ct), list => Results.Ok(list)) : NotFoundProblem();

    [HttpPost("sites/{siteId:guid}/schedules")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("createSchedule")]
    [EndpointSummary("Crear un horario")]
    [EndpointDescription("Ventanas con días (monday … sunday) y horas HH:mm de inicio y fin (fin exclusivo, hasta 24:00).")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create(Guid siteId, ScheduleRequest request, CancellationToken ct) =>
        scope.DenyManage(siteId) ?? AccessResults.ToHttp(await schedules.CreateAsync(siteId, Input(request), ct),
            schedule => Results.Created($"/api/v1/schedules/{schedule.Id}", schedule));

    [HttpGet("schedules/{id:guid}")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("getSchedule")]
    [EndpointSummary("Obtener un horario")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is { } schedule ? Results.Ok(schedule) : NotFoundProblem();

    [HttpPut("schedules/{id:guid}")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("updateSchedule")]
    [EndpointSummary("Modificar un horario")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Update(Guid id, ScheduleRequest request, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } schedule
            ? NotFoundProblem()
            : scope.DenyManage(schedule.SiteId) ?? AccessResults.ToHttp(await schedules.UpdateAsync(id, Input(request), ct), Results.Ok);

    [HttpDelete("schedules/{id:guid}")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("deleteSchedule")]
    [EndpointSummary("Eliminar un horario")]
    [EndpointDescription("Solo si ningún otorgamiento lo usa (409 schedule_in_use).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Delete(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } schedule
            ? NotFoundProblem()
            : scope.DenyManage(schedule.SiteId) ?? AccessResults.ToHttp(await schedules.DeleteAsync(id, ct), _ => Results.NoContent());

    private static ScheduleInput Input(ScheduleRequest request) =>
        new(request.Name!, request.Windows!.Select(w => new ScheduleWindowDto(w.Days ?? [], w.From ?? string.Empty, w.To ?? string.Empty)).ToList());

    private async Task<ScheduleDto?> VisibleAsync(Guid id, CancellationToken ct) =>
        (await schedules.GetAsync(id, ct)).Value is { } schedule && scope.CanRead(schedule.SiteId) ? schedule : null;
}
