using Microsoft.AspNetCore.Mvc;
using Nexgate.Api.Rest.Authorization;
using Nexgate.Api.Rest.Models.Access;
using Nexgate.Common.BuildingBlocks.Security;
using Nexgate.Service.Identity.Contracts;
using Nexgate.Service.Occupancy.Contracts;

namespace Nexgate.Api.Rest.Controllers.Access;

/// <summary>Sujetos de los sitios de la organización activa: la representación local de alguien, sin datos personales.</summary>
[Tags("Subjects")]
[Route(Prefix)]
public sealed class SubjectsController(ISubjectService subjects, ICurrentPermissions permissions, RequestContext context) : ApiControllerBase
{
    private readonly AccessScope scope = new(permissions);

    [HttpGet("sites/{siteId:guid}/subjects")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("listSubjects")]
    [EndpointSummary("Listar los sujetos de un sitio")]
    [EndpointDescription("Ordenados por alias.")]
    [ProducesResponseType<List<SubjectDto>>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> List(Guid siteId, CancellationToken ct) =>
        scope.CanRead(siteId) ? AccessResults.ToHttp(await subjects.ListAsync(siteId, ct), list => Results.Ok(list)) : NotFoundProblem();

    [HttpPost("sites/{siteId:guid}/subjects")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("createSubject")]
    [EndpointSummary("Registrar un sujeto")]
    [EndpointDescription("Persona o anónimo, con un alias. No se cargan datos personales.")]
    [ProducesResponseType<SubjectDto>(StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Create(Guid siteId, SubjectRequest request, CancellationToken ct) =>
        scope.DenyManage(siteId) ?? AccessResults.ToHttp(
            await subjects.CreateAsync(siteId, new SubjectInput(request.Kind!, request.Alias!), context.UserId!.Value, ct),
            subject => Results.Created($"/api/v1/subjects/{subject.Id}", subject));

    [HttpGet("subjects/{id:guid}")]
    [RequirePermission(Permissions.AccessRead)]
    [EndpointName("getSubject")]
    [EndpointSummary("Obtener un sujeto")]
    [ProducesResponseType<SubjectDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is { } subject ? Results.Ok(subject) : NotFoundProblem();

    [HttpPut("subjects/{id:guid}")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("updateSubject")]
    [EndpointSummary("Modificar un sujeto")]
    [ProducesResponseType<SubjectDto>(StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid id, SubjectRequest request, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } subject
            ? NotFoundProblem()
            : scope.DenyManage(subject.SiteId) ?? AccessResults.ToHttp(
                await subjects.UpdateAsync(id, new SubjectInput(request.Kind!, request.Alias!), ct), Results.Ok);

    [HttpDelete("subjects/{id:guid}")]
    [RequirePermission(Permissions.AccessManage)]
    [EndpointName("deleteSubject")]
    [EndpointSummary("Eliminar un sujeto")]
    [EndpointDescription("Solo sin credenciales ni otorgamientos (409 subject_in_use).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound)]
    [ProblemResponse(StatusCodes.Status409Conflict)]
    public async Task<IResult> Delete(Guid id, CancellationToken ct) =>
        await VisibleAsync(id, ct) is not { } subject
            ? NotFoundProblem()
            : scope.DenyManage(subject.SiteId) ?? AccessResults.ToHttp(await subjects.DeleteAsync(id, ct), _ => Results.NoContent());

    private async Task<SubjectDto?> VisibleAsync(Guid id, CancellationToken ct) =>
        (await subjects.GetAsync(id, ct)).Value is { } subject && scope.CanRead(subject.SiteId) ? subject : null;
}
