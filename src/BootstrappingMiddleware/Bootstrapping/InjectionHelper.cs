using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace BootstrappingMiddleware
{
    /// <summary>
    /// Helper class that handles the HTML injection into
    /// a string or byte array.
    /// </summary>
    public static class InjectionHelper
    {
        private const string STR_BodyMarker = "</body>";

        private static readonly byte[] _bodyBytes = Encoding.UTF8.GetBytes(STR_BodyMarker);

        /// <summary>
        /// Adds Live Reload WebSocket script into the page before the body tag.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="context"></param>
        /// <param name="baseStream">The raw Response Stream</param>
        /// <returns></returns>
        public static async Task InjectBootstrapDataAsync(byte[] buffer, HttpContext context, Stream baseStream, ReadOnlyMemory<byte> bootstrapData)
        {
            var index = buffer.LastIndexOf(_bodyBytes);
            if (index == -1)
            {
                await baseStream.WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            var endIndex = index + _bodyBytes.Length;

            // Write pre-marker buffer
            await baseStream.WriteAsync(buffer, 0, index);


            // Write the injected script
            var scriptBytes = Encoding.UTF8.GetBytes(GetWebSocketClientJavaScript(bootstrapData));
            await baseStream.WriteAsync(scriptBytes, 0, scriptBytes.Length);

            // Write the rest of the buffer/HTML doc
            await baseStream.WriteAsync(buffer, endIndex, buffer.Length - endIndex);
        }

        static int LastIndexOf<T>(this T[] array, T[] sought) where T : IEquatable<T> =>
            array.AsSpan().LastIndexOf(sought);

        /// <summary>
        /// Adds Live Reload WebSocket script into the page before the body tag.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="context"></param>
        /// <param name="baseStream">The raw Response Stream</param>
        /// <param name="bootstrapData"></param>
        /// <returns></returns>
        public static Task InjectBootstrapDataAsync(Span<byte> buffer, int offset, int count, HttpContext context, Stream baseStream, ReadOnlyMemory<byte> bootstrapData)
        {
            var curBuffer = buffer.Slice(offset, count).ToArray();
            return InjectBootstrapDataAsync(curBuffer, context, baseStream, bootstrapData);
        }


        public static string GetWebSocketClientJavaScript(ReadOnlyMemory<byte> bootstrapData)
        {
            var data = Encoding.UTF8.GetString(bootstrapData.Span);
            return $@"
<script>
window.config = {data};
</script>
</body>";
        }
    }
}