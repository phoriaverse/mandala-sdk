using UnityEditor.Timeline;

namespace PHORIA.Mandala.SDK.Timeline.Editor
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine.Timeline;
    using UnityEngine;
    using UnityEngine.Playables;

    [InitializeOnLoad]
    public static class TimelinePlayButtonWatcher
    {
        static PlayableDirector _current;

        static TimelinePlayButtonWatcher()
        {
            // Poll because TimelineEditor.inspectedDirector can change as the user switches timelines/directors.
            EditorApplication.update += Update;
        }

        static void Update()
        {
            var d = TimelineEditor.inspectedDirector; 
            if (d == _current) return;

            // Unhook old
            if (_current != null)
            {
                _current.played  -= OnPlayed;
                _current.paused  -= OnPaused;
                _current.stopped -= OnStopped;
            }

            _current = d;

            // Hook new
            if (_current != null)
            {
                _current.played  += OnPlayed;
                _current.paused  += OnPaused;
                _current.stopped += OnStopped;
            }
        }

        static void OnPlayed(PlayableDirector d)
        {
            PHORIA.Mandala.SDK.Timeline.MSDKVideoDriver.NotifyEditorTimelinePlayed(d);
            Debug.Log($"Timeline PLAY clicked. timeUpdateMode={d.timeUpdateMode}, time={d.time}");
        }

        static void OnPaused(PlayableDirector d)
        {
            PHORIA.Mandala.SDK.Timeline.MSDKVideoDriver.NotifyEditorTimelinePaused(d);
            Debug.Log("Timeline PAUSE clicked.");
        }

        static void OnStopped(PlayableDirector d)
        {
            PHORIA.Mandala.SDK.Timeline.MSDKVideoDriver.NotifyEditorTimelineStopped(d);
            Debug.Log("Timeline STOP clicked.");
        }
    }
#endif
}
