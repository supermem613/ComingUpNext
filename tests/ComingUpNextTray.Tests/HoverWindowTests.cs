using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using Xunit;
using ComingUpNextTray;
using ComingUpNextTray.Models;

namespace ComingUpNextTray.Tests {
    public class HoverWindowTests {
        private static string GetTitleText(HoverWindow w) {
            FieldInfo f = typeof(HoverWindow).GetField("titleLabel", BindingFlags.NonPublic | BindingFlags.Instance)!;
            object? lbl = f.GetValue(w);
            PropertyInfo? textProp = lbl?.GetType().GetProperty("Text");
            return (string?)textProp?.GetValue(lbl) ?? string.Empty;
        }

        private static void RaiseDoubleClick(HoverWindow window) {
            MethodInfo onMouseDoubleClick = typeof(Control).GetMethod("OnMouseDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            onMouseDoubleClick.Invoke(window, new object[] { new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0) });
        }

        [Fact]
        public void NumericOverlay_AppendsMin() {
            HoverWindow hw = new HoverWindow();
            CalendarEntry meeting = new CalendarEntry { Title = "Test", StartTime = DateTime.Now.AddMinutes(5), EndTime = DateTime.Now.AddMinutes(65) };
            // Construct the token deterministically: FormatMinutesForIcon returns "5" for 5 minutes,
            // and the hover token should include the unit.
            string tokenWithUnit = ComingUpNextTray.TrayApplication.FormatMinutesForIcon(5) + " min";
            hw.UpdateMeeting(meeting, DateTime.Now, overlayToken: tokenWithUnit);
            string title = GetTitleText(hw);
            Assert.Contains("(in 5 min)", title);
            hw.Dispose();
        }

        [Fact]
        public void HourToken_NotAppendMin() {
            HoverWindow hw = new HoverWindow();
            CalendarEntry meeting = new CalendarEntry { Title = "HourTest", StartTime = DateTime.Now.AddHours(1), EndTime = DateTime.Now.AddHours(2) };
            hw.UpdateMeeting(meeting, DateTime.Now, overlayToken: "1h");
            string title = GetTitleText(hw);
            Assert.Contains("(in 1h)", title);
            hw.Dispose();
        }

        [Fact]
        public void SpecialTokens_AreUnchanged() {
            HoverWindow hw = new HoverWindow();
            CalendarEntry meeting = new CalendarEntry { Title = "Spec", StartTime = DateTime.Now.AddMinutes(10), EndTime = DateTime.Now.AddHours(1) };

            // The implementation explicitly suppresses non-minute symbols like '?' and '-'.
            hw.UpdateMeeting(meeting, DateTime.Now, overlayToken: "?");
            Assert.DoesNotContain("(in ", GetTitleText(hw));

            hw.UpdateMeeting(meeting, DateTime.Now, overlayToken: "-");
            Assert.DoesNotContain("(in ", GetTitleText(hw));

            // Infinity symbol is also suppressed
            hw.UpdateMeeting(meeting, DateTime.Now, overlayToken: UiText.InfiniteSymbol);
            Assert.DoesNotContain("(in ", GetTitleText(hw));

            hw.Dispose();
        }

        [Fact]
        public void VeryLongTitle_IsTruncated() {
            HoverWindow hw = new HoverWindow();
            string longTitle = new string('X', 1000);
            CalendarEntry meeting = new CalendarEntry { Title = longTitle, StartTime = DateTime.Now.AddMinutes(10), EndTime = DateTime.Now.AddHours(1) };
            hw.UpdateMeeting(meeting, DateTime.Now);
            string title = GetTitleText(hw);

            // Expect ellipsis when truncated
            Assert.Contains("...", title);
            hw.Dispose();
        }

        [Fact]
        public void DoubleClick_OpensOnlyTheSuppliedMeetingUrl() {
            List<Uri> openedUrls = new List<Uri>();
            DateTime now = DateTime.Now;
            Uri icsUrl = new Uri("https://calendar.example.test/ics-meeting");
            Uri workIqUrl = new Uri("https://teams.microsoft.com/l/meetup-join/workiq");
            CalendarEntry icsMeeting = new CalendarEntry { Title = "ICS meeting", StartTime = now.AddMinutes(5), EndTime = now.AddMinutes(35), MeetingUrl = icsUrl };
            CalendarEntry workIqMeetingWithoutUrl = new CalendarEntry { Title = "Work IQ meeting without link", StartTime = now.AddMinutes(10), EndTime = now.AddMinutes(40) };
            CalendarEntry workIqMeetingWithUrl = new CalendarEntry { Title = "Work IQ meeting", StartTime = now.AddMinutes(15), EndTime = now.AddMinutes(45), MeetingUrl = workIqUrl };

            using (HoverWindow window = new HoverWindow(openedUrls.Add)) {
                window.UpdateMeeting(icsMeeting, now, meetingUrlToOpen: null);
                RaiseDoubleClick(window);
                window.UpdateMeeting(workIqMeetingWithoutUrl, now, meetingUrlToOpen: null);
                RaiseDoubleClick(window);
                window.UpdateMeeting(workIqMeetingWithUrl, now, meetingUrlToOpen: workIqUrl);
                RaiseDoubleClick(window);
            }

            Assert.Equal(new[] { workIqUrl }, openedUrls);
        }

        [Fact]
        public void DoubleClick_OpensOnlyForWorkIqSource() {
            List<Uri> openedUrls = new List<Uri>();
            DateTime now = DateTime.Now;
            Uri icsUrl = new Uri("https://calendar.example.test/ics-meeting");
            Uri workIqUrl = new Uri("https://teams.microsoft.com/l/meetup-join/workiq");
            Uri? icsUrlToOpen = TrayContext.GetEligibleMeetingUrlForHoverWindow(CalendarSourceKind.Ics, icsUrl);
            Uri? workIqUrlToOpen = TrayContext.GetEligibleMeetingUrlForHoverWindow(CalendarSourceKind.WorkIq, workIqUrl);
            CalendarEntry icsMeeting = new CalendarEntry { Title = "ICS meeting", StartTime = now.AddMinutes(5), EndTime = now.AddMinutes(35), MeetingUrl = icsUrl };
            CalendarEntry workIqMeeting = new CalendarEntry { Title = "Work IQ meeting", StartTime = now.AddMinutes(15), EndTime = now.AddMinutes(45), MeetingUrl = workIqUrl };

            using (HoverWindow window = new HoverWindow(openedUrls.Add)) {
                window.UpdateMeeting(icsMeeting, now, meetingUrlToOpen: icsUrlToOpen);
                RaiseDoubleClick(window);
                window.UpdateMeeting(workIqMeeting, now, meetingUrlToOpen: workIqUrlToOpen);
                RaiseDoubleClick(window);
            }

            Assert.Equal(new[] { workIqUrl }, openedUrls);
        }
    }
}
