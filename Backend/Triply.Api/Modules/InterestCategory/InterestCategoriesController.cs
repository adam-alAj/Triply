using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Modules.InterestCategory.Dtos;

namespace Triply.Api.Modules.InterestCategory;

[ApiController]
[Route("api/interest-categories")]
[Authorize]
[EnableRateLimiting("fixed")]
public class InterestCategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public InterestCategoriesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _db.InterestCategories
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new InterestCategoryResponse
            {
                Id = x.Id,
                Code = x.Code,
                Label = x.Label
            })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }
}
