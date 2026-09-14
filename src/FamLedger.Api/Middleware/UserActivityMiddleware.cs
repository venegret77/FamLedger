using FamLedger.Api.Extensions;
using FamLedger.Domain.Enums;
using FamLedger.Interfaces.Services;

namespace FamLedger.Api.Middleware;

/// <summary>
/// Записывает last-action для любого успешного authenticated API-запроса с активным контекстом.
/// </summary>
public class UserActivityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IUserService userService,
        IUserActivityService activityService)
    {
        await next(httpContext);

        if (httpContext.User.Identity?.IsAuthenticated != true)
            return;
        if (httpContext.Response.StatusCode >= 400)
            return;
        if (!HttpMethods.IsGet(httpContext.Request.Method)
            && !HttpMethods.IsPost(httpContext.Request.Method)
            && !HttpMethods.IsPut(httpContext.Request.Method)
            && !HttpMethods.IsPatch(httpContext.Request.Method)
            && !HttpMethods.IsDelete(httpContext.Request.Method))
            return;

        try
        {
            var userId = httpContext.User.GetUserId();
            var user = await userService.GetByIdAsync(userId, httpContext.RequestAborted);
            if (user?.ActiveContextId is null) return;

            // Транзакции уже пишутся как Transaction в ExpenseService — не дублируем Api поверх.
            var path = httpContext.Request.Path.Value ?? "";
            if (HttpMethods.IsPost(httpContext.Request.Method)
                && path.Contains("/transactions", StringComparison.OrdinalIgnoreCase))
                return;

            await activityService.TouchAsync(
                user.Id, user.ActiveContextId.Value, UserActivityKind.Api, httpContext.RequestAborted);
        }
        catch
        {
            // activity tracking must not break the request
        }
    }
}
