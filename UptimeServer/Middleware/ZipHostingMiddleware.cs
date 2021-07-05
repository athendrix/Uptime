using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace UptimeServer.Middleware
{
    public static class ZipHostingMiddleware
    {
        public static void ZipHosting(this IApplicationBuilder app, CachedZipHost czh)
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.HasValue && context.Request.Path.Value.StartsWith("/"))
                {
                    string path = context.Request.Path.Value.Substring(1);//remove first slash to make web paths align with zip paths

                    //Tries to load content using the base path, and if unsuccessful, tries with index.html on the end, and then with /index.html on the end.
                    //If it can't load anything, then we give up and pass onto the next Middleware.
                    if (await LoadContent(czh,context, path) || await LoadContent(czh,context,path + "index.html") || await LoadContent(czh,context,path + "/index.html"))
                    {
                        return;
                    }
                }
                // Do work that doesn't write to the Response.
                await next.Invoke();
                // Do logging or other work that doesn't write to the Response.
            });
        }
        public static void SinglePageZipHosting(this IApplicationBuilder app, CachedZipHost czh)
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.HasValue && context.Request.Path.Value.StartsWith("/"))
                {
                    string path = context.Request.Path.Value.Substring(1);//remove first slash to make web paths align with zip paths
                    if(!path.StartsWith("api",StringComparison.InvariantCultureIgnoreCase))//skips the magic if they're trying to hit the api.
                    {
                        //
                        if (await LoadContent(czh, context, path) || (!path.Contains(".") && await LoadContent(czh, context, "index.html")))
                        {
                            return;
                        }
                    }
                }
                // Do work that doesn't write to the Response.
                await next.Invoke();
                // Do logging or other work that doesn't write to the Response.
            });
        }
        private static async Task<bool> LoadContent(CachedZipHost czh, HttpContext context, string path)
        {
            if(czh.ContainsContent(path))
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = MimeTypeMap.GetMimeType(Path.GetExtension(path));
                using MemoryStream ms = czh.GetContent(path);
                await ms.CopyToAsync(context.Response.Body);
                return true;
            }
            return false;
        }
    }
}
