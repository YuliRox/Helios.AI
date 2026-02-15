using LumiRise.Api.Data;
using LumiRise.Api.Data.Entities;
using LumiRise.Api.Models.Ramps;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LumiRise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RampsController(LumiRiseDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<RampResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RampResponse>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var ramps = await dbContext.RampProfiles
            .AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return Ok(ramps.Select(MapResponse).ToArray());
    }

    [HttpGet("{id:guid}", Name = "GetRampById")]
    [ProducesResponseType(typeof(RampResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RampResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var ramp = await dbContext.RampProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return ramp is null ? NotFound() : Ok(MapResponse(ramp));
    }

    [HttpPost]
    [ProducesResponseType(typeof(RampResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RampResponse>> CreateAsync(
        [FromBody] RampUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateRequest(request, out var normalizedMode))
        {
            return ValidationProblem(ModelState);
        }

        var exists = await dbContext.RampProfiles
            .AsNoTracking()
            .AnyAsync(x => x.Mode == normalizedMode, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(request.Mode), $"Ramp mode '{normalizedMode}' already exists.");
            return ValidationProblem(ModelState);
        }

        var utcNow = DateTime.UtcNow;
        var entity = new RampProfileEntity
        {
            Mode = normalizedMode,
            StartBrightnessPercent = request.StartBrightnessPercent,
            TargetBrightnessPercent = request.TargetBrightnessPercent,
            RampDurationSeconds = request.RampDurationSeconds,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        dbContext.RampProfiles.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtRoute("GetRampById", new { id = entity.Id }, MapResponse(entity));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RampResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RampResponse>> UpdateAsync(
        Guid id,
        [FromBody] RampUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateRequest(request, out var normalizedMode))
        {
            return ValidationProblem(ModelState);
        }

        var entity = await dbContext.RampProfiles
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var existingMode = await dbContext.RampProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Mode == normalizedMode && x.Id != id,
                cancellationToken);
        if (existingMode is not null)
        {
            ModelState.AddModelError(nameof(request.Mode), $"Ramp mode '{normalizedMode}' already exists.");
            return ValidationProblem(ModelState);
        }

        entity.Mode = normalizedMode;
        entity.StartBrightnessPercent = request.StartBrightnessPercent;
        entity.TargetBrightnessPercent = request.TargetBrightnessPercent;
        entity.RampDurationSeconds = request.RampDurationSeconds;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapResponse(entity));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.RampProfiles
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var inUse = await dbContext.AlarmSchedules
            .AsNoTracking()
            .AnyAsync(x => x.RampProfileId == id, cancellationToken);
        if (inUse)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Ramp profile is in use.",
                Detail = $"Ramp profile '{entity.Mode}' is assigned to one or more alarms."
            });
        }

        dbContext.RampProfiles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool ValidateRequest(RampUpsertRequest request, out string normalizedMode)
    {
        normalizedMode = string.Empty;

        if (string.IsNullOrWhiteSpace(request.Mode))
        {
            ModelState.AddModelError(nameof(request.Mode), "Mode is required.");
            return false;
        }

        normalizedMode = request.Mode.Trim().ToLowerInvariant();
        if (normalizedMode.Length > 100)
        {
            ModelState.AddModelError(nameof(request.Mode), "Mode cannot exceed 100 characters.");
            return false;
        }

        return ModelState.IsValid;
    }

    private static RampResponse MapResponse(RampProfileEntity entity) => new()
    {
        Id = entity.Id,
        Mode = entity.Mode,
        StartBrightnessPercent = entity.StartBrightnessPercent,
        TargetBrightnessPercent = entity.TargetBrightnessPercent,
        RampDurationSeconds = entity.RampDurationSeconds,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc
    };
}
