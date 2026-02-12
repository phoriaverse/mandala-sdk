namespace PHORIA.Mandala.SDK.Timeline
{
	using System;
	using UnityEngine;

	public interface IVideoTextureSource
	{
		Texture[] Textures { get; }
		PixelFormat PixelFormat { get; }
		 ColorSpace ColorSpace { get; }

		void SetTextures(Texture[] textures);
		void SetPixelFormat(PixelFormat format);
		void SetColorSpace(ColorSpace colorSpace);
		event Action TexturesChanged;
	}
	
	
	public enum PixelFormat { RGBA, NV12, YUV420P }

	public enum ColorSpace
	{
		BT709,
		BT601,
		BT2020
	}


}
