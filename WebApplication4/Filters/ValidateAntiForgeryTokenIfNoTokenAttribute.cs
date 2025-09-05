using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebApplication4.Filters
{
    public class ValidateAntiForgeryTokenIfNoTokenAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var token = filterContext.HttpContext.Request["token"];
            if (string.IsNullOrEmpty(token))
            {
                // Only validate anti-forgery if no token (i.e., in-app)
                var validator = new ValidateAntiForgeryTokenAttribute();
                validator.OnAuthorization(filterContext);
            }
            // else skip validation for email link
        }
    }
}