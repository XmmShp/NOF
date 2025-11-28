using Microsoft.AspNetCore.Http;

namespace NOF.Test;

public static class HttpContextExtensions
{
    extension(HttpContext context)
    {
        public async Task<string> GetResponseAsStringAsync()
        {
            await context.Response.Body.FlushAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            using var reader = new StreamReader(context.Response.Body);
            return await reader.ReadToEndAsync();
        }

        public static DefaultHttpContext CreateTestHttpContext()
        {
            var defaultContext = new DefaultHttpContext();
            var bodyStream = new MemoryStream();
            defaultContext.Response.Body = bodyStream;
            return defaultContext;
        }
    }
}
