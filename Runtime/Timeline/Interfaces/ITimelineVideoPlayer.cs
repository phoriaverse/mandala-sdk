using UnityEngine;


namespace PHORIA.Mandala.SDK.Timeline
{

	/// <summary>
    /// Abstraction for a timeline-driven video backend.
    ///
    /// The Timeline layer (e.g. <see cref="VideoTimelineLink"/>) depends on this interface so we can swap
    /// implementations:
    /// - Runtime: TiledMedia-backed playback
    /// - Editor scrubbing: Unity VideoPlayer-backed texture generation
    ///
    /// Contract:
    /// - Time is expressed in seconds in the same reference frame as the Timeline (typically content time).
    /// - Implementations should be safe to call every frame; failures should return false (not throw).
    ///
    /// TODO : for more accurate tiled control/updates we should prob make these async
    /// 
    /// </summary>
    public interface ITimelineVideoPlayer
    {
        /// <summary>
        /// True when the backend is actively playing (and not currently seeking).
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// True when the backend is currently seeking/buffering such that time/progress should not be driven.
        /// </summary>
        bool IsSeeking { get; }

        /// <summary>
        /// Try to get current playback time in seconds.
        /// </summary>
        bool TryGetTimeSeconds(out double timeSeconds);

        /// <summary>
        /// Normalized progress 0..1 when known; returns false when unknown.
        /// </summary>
        bool TryGetNormalizedProgress(out float normalized);

        /// <summary>
        /// Request playback for the given asset data. Implementations may ignore if already playing the same asset.
        /// </summary>
        bool Play(MSDKVideoClipData clipData);
        
        /// <summary>
        /// Seek to the given time in seconds.
        /// </summary>
        bool Seek(double timeSeconds);
        
        bool PreviewFrame(double timeSeconds);
    }
}
