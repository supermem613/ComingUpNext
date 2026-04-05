namespace ComingUpNextTray.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Plays an MP3 sound intro timed to end exactly when a target time arrives.
    /// Uses Windows Media Player COM object for reliable MP3 support on modern Windows.
    /// All COM calls execute on the creating thread (UI/STA) to satisfy COM apartment rules.
    /// Suppresses playback when any microphone has an active capture session (e.g. Teams, Zoom).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2213:Disposable fields should be disposed", Justification = "scheduleCts is disposed via CancelSchedule() called from Stop() in Dispose.")]
    internal sealed class SoundIntroService : IDisposable
    {
        private const int DeviceStateActive = 0x00000001;
        private const int ClsctxAll = 0x17;
        private static readonly Type? WmpType = Type.GetTypeFromProgID("WMPlayer.OCX");
        private readonly SynchronizationContext? syncContext;
        private CancellationTokenSource? scheduleCts;
        private dynamic? activePlayer;
        private bool disposed;
        private string? cachedDurationPath;
        private long cachedDurationMs = -1;

        /// <summary>Initializes a new instance of the <see cref="SoundIntroService"/> class.</summary>
        internal SoundIntroService()
        {
            this.syncContext = SynchronizationContext.Current;
        }

        /// <summary>Audio data-flow direction for Core Audio endpoint enumeration.</summary>
        private enum EDataFlow
        {
            /// <summary>Audio rendering (playback) endpoint.</summary>
            Render = 0,

            /// <summary>Audio capture (recording) endpoint.</summary>
            Capture = 1,
        }

#pragma warning disable SA1600 // Elements should be documented — COM vtable-mapping interfaces need slot declarations, not API docs.
#pragma warning disable SA1611 // Element parameters should be documented
#pragma warning disable SA1615 // Element return value should be documented

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, int role, out IMMDevice device);
        }

        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig]
            int GetCount(out int count);

            [PreserveSig]
            int Item(int index, out IMMDevice device);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig]
            int GetAudioSessionControl();

            [PreserveSig]
            int GetSimpleAudioVolume();

            [PreserveSig]
            int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig]
            int GetCount(out int sessionCount);

            [PreserveSig]
            int GetSession(int sessionIndex, out IAudioSessionControl session);
        }

        [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl
        {
            [PreserveSig]
            int GetState(out int state);
        }

#pragma warning restore SA1615
#pragma warning restore SA1611
#pragma warning restore SA1600

        /// <summary>Disposes managed and unmanaged resources.</summary>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.Stop();
            this.scheduleCts?.Dispose();
            this.scheduleCts = null;
        }

        /// <summary>
        /// Gets the duration of an MP3 file in milliseconds. Results are cached by path.
        /// </summary>
        /// <param name="mp3Path">Absolute path to the MP3 file.</param>
        /// <returns>Duration in milliseconds, or -1 if the file cannot be read.</returns>
        internal long GetDurationMs(string mp3Path)
        {
            return this.GetDurationMs(mp3Path, out _);
        }

        /// <summary>
        /// Gets the duration of an MP3 file in milliseconds with diagnostic info.
        /// </summary>
        /// <param name="mp3Path">Absolute path to the MP3 file.</param>
        /// <param name="diagnostic">Diagnostic message if duration cannot be read.</param>
        /// <returns>Duration in milliseconds, or -1 if the file cannot be read.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "COM interop with dynamic dispatch can throw any exception type.")]
        internal long GetDurationMs(string mp3Path, out string? diagnostic)
        {
            diagnostic = null;

            if (string.IsNullOrWhiteSpace(mp3Path))
            {
                diagnostic = "Path is empty.";
                return -1;
            }

            if (!File.Exists(mp3Path))
            {
                diagnostic = $"File not found: {mp3Path}";
                return -1;
            }

            // Return cached result if same path.
            if (string.Equals(mp3Path, this.cachedDurationPath, StringComparison.OrdinalIgnoreCase) && this.cachedDurationMs > 0)
            {
                return this.cachedDurationMs;
            }

            if (WmpType is null)
            {
                diagnostic = "Windows Media Player COM object (WMPlayer.OCX) not available.";
                return -1;
            }

            try
            {
                dynamic player = Activator.CreateInstance(WmpType) !;
                try
                {
                    dynamic media = player.newMedia(mp3Path);
                    double seconds = media.duration;
                    if (seconds <= 0)
                    {
                        diagnostic = $"WMP returned duration={seconds}s for: {mp3Path}";
                        return -1;
                    }

                    long ms = (long)(seconds * 1000);
                    this.cachedDurationPath = mp3Path;
                    this.cachedDurationMs = ms;
                    return ms;
                }
                finally
                {
                    player.close();
                }
            }
            catch (Exception ex)
            {
                diagnostic = $"WMP error: {ex.GetType().Name}: {ex.Message}";
                return -1;
            }
        }

        /// <summary>
        /// Schedules playback of an MP3 so that it finishes at <paramref name="targetEndTime"/>.
        /// Uses the pre-computed <paramref name="durationMs"/> to avoid redundant queries.
        /// If the playback window has already passed, does nothing.
        /// Cancels any previously scheduled playback.
        /// </summary>
        /// <param name="mp3Path">Absolute path to the MP3 file.</param>
        /// <param name="targetEndTime">The time at which playback should end (meeting start).</param>
        /// <param name="durationMs">Pre-computed duration of the MP3 in milliseconds.</param>
        internal void SchedulePlayback(string mp3Path, DateTime targetEndTime, long durationMs)
        {
            // Cancel any pending schedule.
            this.CancelSchedule();

            if (durationMs <= 0)
            {
                return;
            }

            double delayMs = (targetEndTime - DateTime.Now).TotalMilliseconds - durationMs;
            if (delayMs < 0)
            {
                // Window has passed; do not play.
                return;
            }

            this.scheduleCts = new CancellationTokenSource();
            CancellationToken ct = this.scheduleCts.Token;

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        if (delayMs > 0)
                        {
                            await Task.Delay((int)Math.Min(delayMs, int.MaxValue), ct).ConfigureAwait(false);
                        }

                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        // Marshal Play to the UI thread for COM apartment compatibility.
                        this.PostToOriginThread(() => this.Play(mp3Path));
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected on cancellation.
                    }
                },
                ct);
        }

        /// <summary>
        /// Plays the MP3 immediately (for testing). Must be called on the UI thread.
        /// </summary>
        /// <param name="mp3Path">Absolute path to the MP3 file.</param>
        internal void PlayNow(string mp3Path)
        {
            this.Play(mp3Path);
        }

        /// <summary>
        /// Stops any in-progress or scheduled playback.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "COM interop with dynamic dispatch can throw any exception type.")]
        internal void Stop()
        {
            this.CancelSchedule();
            if (this.activePlayer is not null)
            {
                try
                {
                    this.activePlayer.controls.stop();
                    this.activePlayer.close();
                }
                catch (Exception)
                {
                    // Best-effort cleanup.
                }

                this.activePlayer = null;
            }
        }

        /// <summary>
        /// Returns true if any audio capture device has an active recording session
        /// (e.g. Teams, Zoom). Returns false when detection fails (fail-open).
        /// </summary>
        /// <returns>True if a microphone is actively being used by an application.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "COM interop can throw unpredictable exceptions; fail-open is intentional.")]
        private static bool IsMicrophoneInUse()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();

                int hr = enumerator.EnumAudioEndpoints(EDataFlow.Capture, DeviceStateActive, out var collection);
                if (hr != 0 || collection is null)
                {
                    return false;
                }

                collection.GetCount(out int deviceCount);
                for (int d = 0; d < deviceCount; d++)
                {
                    hr = collection.Item(d, out var device);
                    if (hr != 0 || device is null)
                    {
                        continue;
                    }

                    Guid iid = typeof(IAudioSessionManager2).GUID;
                    hr = device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out object? activated);
                    if (hr != 0 || activated is null)
                    {
                        continue;
                    }

                    var sessionManager = (IAudioSessionManager2)activated;
                    hr = sessionManager.GetSessionEnumerator(out var sessionEnum);
                    if (hr != 0 || sessionEnum is null)
                    {
                        continue;
                    }

                    sessionEnum.GetCount(out int sessionCount);
                    for (int s = 0; s < sessionCount; s++)
                    {
                        hr = sessionEnum.GetSession(s, out var session);
                        if (hr != 0 || session is null)
                        {
                            continue;
                        }

                        session.GetState(out int state);
                        if (state == 1)
                        {
                            // AudioSessionStateActive
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "COM interop with dynamic dispatch can throw any exception type.")]
        private void Play(string mp3Path)
        {
            this.Stop();

            if (WmpType is null)
            {
                return;
            }

            if (IsMicrophoneInUse())
            {
                return;
            }

            try
            {
                dynamic player = Activator.CreateInstance(WmpType) !;
                player.URL = mp3Path;
                player.controls.play();
                this.activePlayer = player;
            }
            catch (Exception)
            {
                // Silently fail — audio is non-critical.
            }
        }

        private void PostToOriginThread(Action action)
        {
            if (this.syncContext is not null)
            {
                this.syncContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }

        private void CancelSchedule()
        {
            if (this.scheduleCts is not null)
            {
                this.scheduleCts.Cancel();
                this.scheduleCts.Dispose();
                this.scheduleCts = null;
            }
        }

        /// <summary>COM coclass for the multimedia device enumerator.</summary>
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorClass
        {
        }
    }
}
