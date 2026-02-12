namespace PHORIA.Mandala.SDK.Timeline
{
	using System;
	using UnityEngine;

	public sealed class MSDKVideoTextureStore : IVideoTextureSource
	{
		private Texture[] _textures = Array.Empty<Texture>();

		public Texture[] Textures => _textures;
		public ColorSpace ColorSpace { get; private set; } = ColorSpace.BT709;
		public PixelFormat PixelFormat { get; private set; } = PixelFormat.YUV420P;
		
		public event Action TexturesChanged;

		public void SetTextures(Texture[] textures)
		{
			_textures = textures ?? Array.Empty<Texture>();

			//set global shader properties for direct access in shaders
			Shader.SetGlobalTexture("_MSDK_VideoTex0", _textures.Length > 0 ? _textures[0]: null);
			Shader.SetGlobalTexture("_MSDK_VideoTex1", _textures.Length > 1 ? _textures[1]: null);
			Shader.SetGlobalTexture("_MSDK_VideoTex2", _textures.Length > 2 ? _textures[2]: null);
			Shader.SetGlobalFloat("_MSDK_VideoTime", 0f);
			
			TexturesChanged?.Invoke();
		}
		
		public void SetPixelFormat(PixelFormat format)
		{
			PixelFormat = format;
		}

		public void SetColorSpace(ColorSpace colorSpace)
		{
			ColorSpace = colorSpace;
		}
	}
}
