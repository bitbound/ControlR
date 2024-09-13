using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Data;
public class AppDb(DbContextOptions<AppDb> options)
    : IdentityDbContext<AppUser>(options)
{
}
