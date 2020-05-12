using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace BootstrappingMiddleware
{
    public class BootsrappingMiddleware
    {
        private readonly RequestDelegate _next;


        public BootsrappingMiddleware(RequestDelegate next)
        {
            _next = next;
        }


        /// <summary>
        /// Routes to WebSocket handler and injects javascript into
        /// HTML content
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>

        public async Task InvokeAsync(HttpContext context)
        {
            await HandleHtmlInjection(context);
        }



        /// <summary>
        /// Inspects all non WebSocket content for HTML documents
        /// and if it finds HTML injects the JavaScript needed to
        /// refresh the browser via Web Sockets.
        ///
        /// Uses a wrapper stream to wrap the response and examine
        /// only text/html requests - other content is passed through
        /// as is.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task HandleHtmlInjection(HttpContext context)
        {
            var path = context.Request.Path.Value;

            // Use a custom StreamWrapper to rewrite output on Write/WriteAsync
            using (var filteredResponse = new ResponseStreamWrapper(context.Response.Body, context))
            {
#if !NETCORE2
                // Use new IHttpResponseBodyFeature for abstractions of pilelines/streams etc.
                // For 3.x this works reliably while direct Response.Body was causing random HTTP failures
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(filteredResponse));

                //var feature = context.Features.Get<IHttpResponseBodyFeature>();
#else
                context.Response.Body = filteredResponse;
#endif

                // first request to be accepted and processed my MVC middleware
                await _next(context);

                // inspect if MVC was able to include bootstrapped details, if it was we need
                // to re-run the rest of the middlewares to modify the final request
                if (context.Items.ContainsKey("bootstrapped"))
                {
                    // in our forwarded call, since we manipulate the response, we're going to
                    // want it to be in plain text
                    context.Request.Headers.Remove(HeaderNames.AcceptEncoding);
                    await _next(context);
                }
            }
        }
    }
}