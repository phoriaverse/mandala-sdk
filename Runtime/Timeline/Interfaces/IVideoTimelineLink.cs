using UnityEngine;

namespace PHORIA.Mandala.SDK.Timeline
{

	public interface IVideoTimelineLink
	{
		bool RequestPlayableClip(MSDKVideoClipData clipData); 
		bool IsReady { get; } 
	}
}
