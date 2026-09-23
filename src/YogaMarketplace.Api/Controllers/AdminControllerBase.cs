using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YogaMarketplace.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public abstract class AdminControllerBase : ControllerBase
{
}
