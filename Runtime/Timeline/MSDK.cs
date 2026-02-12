namespace PHORIA.Mandala.SDK.Timeline
{
	using System.Collections.Generic;
	using UnityEngine;

	/// <summary>
	/// Single static registry for MSDK runtime state.
	/// </summary>
	public static class MSDK
	{

		private static readonly MSDKVideoTextureStore _video = new();
		public static IVideoTextureSource Video => _video;

	}
}
