using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests
{
    public sealed class ConditionalFetchTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                this.responder = responder;
            }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(this.responder(request));
            }
        }

        [Fact]
        public async Task FirstFetch_NoValidators_ReturnsEntries()
        {
            const string ics = "BEGIN:VEVENT\nDTSTART:20250101T130000Z\nEND:VEVENT";
            using StubHandler handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ics),
                Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"") }
            });
            using CalendarService service = new CalendarService(handler);
            IReadOnlyList<ComingUpNextTray.Models.CalendarEntry> entries = await service.FetchIfChangedAsync(new Uri("https://example.com/calendar.ics"));
            Assert.NotEmpty(entries);
            Assert.True(service.HasChangeValidators);
        }

        [Fact]
        public async Task SecondFetch_NotModified_ReturnsEmpty()
        {
            int call = 0;
            using StubHandler handler = new StubHandler(_ =>
            {
                call++;
                if (call == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("BEGIN:VEVENT\nDTSTART:20250101T130000Z\nEND:VEVENT"),
                        Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v2\"") }
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotModified)
                {
                    Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v2\"") }
                };
            });
            using CalendarService service = new CalendarService(handler);
            IReadOnlyList<ComingUpNextTray.Models.CalendarEntry> first = await service.FetchIfChangedAsync(new Uri("https://example.com/calendar.ics"));
            Assert.NotEmpty(first);
            IReadOnlyList<ComingUpNextTray.Models.CalendarEntry> second = await service.FetchIfChangedAsync(new Uri("https://example.com/calendar.ics"));
            Assert.Empty(second); // indicates unchanged
        }

        [Fact]
        public async Task Modified_ReturnsNewEntries()
        {
            int call = 0;
            using StubHandler handler = new StubHandler(_ =>
            {
                call++;
                if (call == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("BEGIN:VEVENT\nDTSTART:20250101T130000Z\nEND:VEVENT"),
                        Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v3\"") }
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("BEGIN:VEVENT\nDTSTART:20250102T130000Z\nEND:VEVENT"),
                    Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v4\"") }
                };
            });
            using CalendarService service = new CalendarService(handler);
            IReadOnlyList<ComingUpNextTray.Models.CalendarEntry> first = await service.FetchIfChangedAsync(new Uri("https://example.com/calendar.ics"));
            IReadOnlyList<ComingUpNextTray.Models.CalendarEntry> second = await service.FetchIfChangedAsync(new Uri("https://example.com/calendar.ics"));
            Assert.Single(first);
            Assert.Single(second);
            Assert.NotEqual(first[0].StartTime, second[0].StartTime);
        }
    }
}
