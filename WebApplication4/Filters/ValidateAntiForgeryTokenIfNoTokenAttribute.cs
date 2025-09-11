using System;
using System.Web;
using System.Web.Mvc;

namespace WebApplication4.Filters
{
    public class ValidateAntiForgeryTokenIfNoTokenAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            // Skip validation for Hangfire requests
            var path = filterContext.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(path) && path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Skip validation if this is a Hangfire or server-side call (no HTTP form)
            if (filterContext.HttpContext.Request == null || filterContext.HttpContext.Request.HttpMethod != "POST")
            {
                return;
            }

            var token = filterContext.HttpContext.Request["token"];
            if (string.IsNullOrEmpty(token))
            {
                // Only validate anti-forgery if no token (i.e., normal form submit)
                var validator = new ValidateAntiForgeryTokenAttribute();
                validator.OnAuthorization(filterContext);
            }
        }
    }
}
