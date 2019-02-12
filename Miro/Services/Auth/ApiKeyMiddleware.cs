using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Miro.Services.Auth
{
    public class ApiKeyMiddleware
    {
         private readonly RequestDelegate next;
        private readonly IConfiguration configuration;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            this.next = next;
            this.configuration = configuration;
        }

         public async Task Invoke(HttpContext context)
        {
            // Skip isAlive
            if (context.Request.Path.Value.Contains("api/isAlive"))  
            {  
                await next.Invoke(context);
                return;  
            }  

            var apiKey = configuration.GetValue<string>("API_KEY");

            if (apiKey == null)
            {
                 await next.Invoke(context);
                 return;
            }

            var reqApiKey = context.Request.Headers["Authorization"];
            if (apiKey == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Api Key is missing");
                return;
            }
            if (reqApiKey != apiKey)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Api Key is invalid");
                return;
            }

             await next.Invoke(context);
        }
    }

    public static class ApiKeyMiddlewareExt
    {
        public static IApplicationBuilder ApplyApiKeyValidation(this IApplicationBuilder app)
        {
            app.UseMiddleware<ApiKeyMiddleware>();
            return app;
        }
    }
}