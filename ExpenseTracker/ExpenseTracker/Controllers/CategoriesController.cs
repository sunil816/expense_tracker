using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken) =>
        Ok(await categoryService.ListAsync(cancellationToken));
}
