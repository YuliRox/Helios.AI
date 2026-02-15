using Hangfire.Dashboard;

namespace LumiRise.Api.Infrastructure;

public sealed class AllowAllHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
