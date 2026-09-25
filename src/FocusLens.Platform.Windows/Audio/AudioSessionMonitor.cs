using FocusLens.Core.Meetings.Detection;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace FocusLens.Platform.Windows.Audio;

/// <summary>
/// Finds processes that are actively capturing the microphone by enumerating WASAPI
/// audio sessions on every active capture device.
/// </summary>
public sealed class AudioSessionMonitor : IAudioProcessMonitor
{
    public IReadOnlyList<AudioProcessState> CapturingProcesses()
    {
        var found = new Dictionary<int, AudioProcessState>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                using (device)
                {
                    var sessions = device.AudioSessionManager.Sessions;
                    for (var i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];
                        if (session.State != AudioSessionState.AudioSessionStateActive) continue;
                        var pid = (int)session.GetProcessID;
                        if (pid <= 0) continue;
                        found[pid] = new AudioProcessState(pid, IsRunningInput: true, IsRunningOutput: false);
                    }
                }
            }
        }
        catch
        {
            // Device enumeration can fail transiently while devices change; report nothing this tick.
        }
        return found.Values.ToList();
    }
}
