﻿using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sharpbrake.Client;

namespace Sharpbrake.Http.Module
{
    /// <summary>
    /// Implementation of <see cref="IHttpContext"/> based on standard ASP.NET HttpContext.
    /// </summary>
    public class AspNetHttpContext : IHttpContext
    {
        public IDictionary<string, string> Session { get; set; }
        public IDictionary<string, string> Parameters { get; set; }
        public IDictionary<string, string> EnvironmentVars { get; set; }

        public string UserAgent { get; set; }
        public string Url { get; set; }

        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public string UserName { get; set; }

        public string Action { get; set; }
        public string Component { get; set; }

        public AspNetHttpContext(HttpContext context)
        {
            var request = context.Request;

            // TODO: Consider how to initialize user's email and id from context
            UserEmail = null;
            UserId = null;

            Url = request.Url.AbsoluteUri;
            UserAgent = request.UserAgent;

            UserName = context.User != null && context.User.Identity.IsAuthenticated
                ? context.User.Identity.Name
                : null;

            if (request.Headers.HasKeys())
            {
                EnvironmentVars = request.Headers.Keys.Cast<string>()
                    .ToDictionary(key => key, key => request.Headers[key]);
            }

            if (request.Params.HasKeys())
            {
                Parameters = request.Params.Keys.Cast<string>()
                    .ToDictionary(key => key, key => request.Params[key]);
            }

            if (context.Session != null && context.Session.Keys.Count > 0)
            {
                Session = context.Session.Keys.Cast<string>()
                    .ToDictionary(key => key, key => context.Session[key].ToString());
            }
        }
    }
}
