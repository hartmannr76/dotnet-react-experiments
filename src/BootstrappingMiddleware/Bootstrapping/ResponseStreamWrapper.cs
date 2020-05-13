using System;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace BootstrappingMiddleware
{
    public class ResponseStreamWrapper : Stream
    {
        private Stream _baseStream;
        private HttpContext _context;

        private ReadOnlyMemory<byte> _tempBuffer = null;

        private bool _isContentLengthSet = false;

        public ResponseStreamWrapper(Stream baseStream, HttpContext context)
        {
            _baseStream = baseStream;
            _context = context;
            CanWrite = true;
        }

        public override void Flush() => _baseStream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            // this is called at the beginning of a request in 3.x and so
            // we have to set the ContentLength here as the flush/write locks headers
            if (!_isContentLengthSet && IsHtmlResponse())
            {
                _context.Response.Headers.ContentLength = null;
                _isContentLengthSet = true;
            }

            return _baseStream.FlushAsync(cancellationToken);
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }


        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
            IsHtmlResponse();
            IsBootstrappedJsonResponse();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _baseStream.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            _baseStream.WriteByte(value);
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (IsBootstrappedJsonResponse())
            {
                _tempBuffer = buffer;
                _tempBuffer = _tempBuffer.Slice(offset, count);
            }
            else if (IsHtmlResponse())
            {
                InjectionHelper.InjectBootstrapDataAsync(
                    buffer, offset, count,
                    _context, _baseStream, _tempBuffer).GetAwaiter().GetResult();
            }
            else
            {
                _baseStream.Write(buffer, offset, count);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count,
                                              CancellationToken cancellationToken)
        {
            if (IsBootstrappedJsonResponse())
            {
                _tempBuffer = buffer;
                _tempBuffer = _tempBuffer.Slice(offset, count);
            }
            else if (IsHtmlResponse())
            {
                await InjectionHelper.InjectBootstrapDataAsync(
                buffer, offset, count,
                _context, _baseStream, _tempBuffer);
            }
            else
            {
                await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            }
        }

        private bool IsHtmlResponse()
        {
            var isHtmlResponse =
                _context.Response.StatusCode == 200 &&
                _context.Response.ContentType != null &&
                _context.Response.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
                (_context.Response.ContentType.Contains("utf-8", StringComparison.OrdinalIgnoreCase) ||
                !_context.Response.ContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase));

            // Make sure we force dynamic content type since we're
            // rewriting the content - static content will set the header explicitly
            // and fail when it doesn't matchif (_isHtmlResponse.Value)
            if (!_isContentLengthSet && _context.Response.ContentLength != null)
            {
                _context.Response.Headers.ContentLength = null;
                _isContentLengthSet = true;
            } 
                
            return isHtmlResponse;
        }
        
        private bool IsBootstrappedJsonResponse()
        {
            var isBootstrapData = _context.Items.ContainsKey("bootstrapped");

            if (!isBootstrapData)
            {
                return false;
            }
            
            var isJson =
                _context.Response.StatusCode == 200 &&
                _context.Response.ContentType != null &&
                _context.Response.ContentType.Contains(MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase) &&
                (_context.Response.ContentType.Contains("utf-8", StringComparison.OrdinalIgnoreCase) ||
                 !_context.Response.ContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase));

            // Make sure we force dynamic content type since we're
            // rewriting the content - static content will set the header explicitly
            // and fail when it doesn't matchif (_isHtmlResponse.Value)
            if (!_isContentLengthSet && _context.Response.ContentLength != null)
            {
                _context.Response.Headers.ContentLength = null;
                _isContentLengthSet = true;
            } 
                
            return isJson;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (IsBootstrappedJsonResponse())
            {
                _tempBuffer = buffer;
            }
            else if (IsHtmlResponse())
            {
                await InjectionHelper.InjectBootstrapDataAsync(buffer.ToArray(), _context, _baseStream, _tempBuffer);
            }
            else
            {
                await _baseStream.WriteAsync(buffer, cancellationToken);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _baseStream?.Dispose();
            _baseStream = null;
            _context = null;

            base.Dispose(disposing);
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }
    }
}