using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests
{
    public class ConditionalFetchFallbackTests
    {
        private const string SampleIcs = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nSUMMARY:Test Event\r\nDTSTART:20250101T120000Z\r\nDTEND:20250101T130000Z\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

        [Fact]
        public async Task FetchIfChanged_ReturnsEmpty_On304_Then_FullFetchReturnsEntries()
        {
            int call = 0;
            using var handler = new DelegatingHandlerStub((req, ct) =>
            {
                call++;
                if (call == 1)
                {
                    // Simulate conditional request responded with 304
                    var resp = new HttpResponseMessage(HttpStatusCode.NotModified);
                    return Task.FromResult(resp);
                }

                // Subsequent full GET returns 200 with ICS
                var ok = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleIcs)
                };
                return Task.FromResult(ok);
            });

            using var service = new CalendarService(handler);
            var uri = new System.Uri("https://example.com/calendar.ics");

            // First: conditional fetch should yield empty set (304)
            var changed = await service.FetchIfChangedAsync(uri);
            Assert.Empty(changed);

            // Then: full fetch should return entries parsed from the ICS
            var full = await service.FetchWithErrorsAsync(uri);
            Assert.NotEmpty(full);
            Assert.Contains(full, e => e.Title != null && e.Title.Contains("Test Event"));
        }
    }

    internal class DelegatingHandlerStub : DelegatingHandler
    {
        private readonly System.Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;

        public DelegatingHandlerStub(System.Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return this.responder(request, cancellationToken);
        }
    }
}
