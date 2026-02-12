namespace PHORIA.Mandala.SDK.Timeline
{
	using System;
	using System.IO;
	using UnityEngine;

	public static class ResolveVideoUrl
	{
		public static bool TryResolve(string input, out string url)
		{
			url = null;
			if (string.IsNullOrWhiteSpace(input))
				return false;

			var trimmed = input.Trim();
			if (LooksLikeUrl(trimmed))
			{
				url = trimmed;
				return true;
			}

			if (Path.IsPathRooted(trimmed))
			{
				url = ToFileUrl(trimmed);
				return true;
			}

			if (TryResolveRelative(trimmed, out url))
				return true;

			url = trimmed;
			return true;
		}

		public static string ResolveOrNull(string input)
		{
			return TryResolve(input, out var url) ? url : null;
		}

		private static bool TryResolveRelative(string relative, out string url)
		{
			var streaming = Application.streamingAssetsPath;
			if (!string.IsNullOrEmpty(streaming))
			{
				var candidate = CombineBaseAndRelative(streaming, relative);
				if (LooksLikeUrl(candidate))
				{
					url = candidate;
					return true;
				}

				if (File.Exists(candidate))
				{
					url = ToFileUrl(candidate);
					return true;
				}
			}

			var persistent = Application.persistentDataPath;
			if (!string.IsNullOrEmpty(persistent))
			{
				var candidate = Path.Combine(persistent, relative);
				if (File.Exists(candidate))
				{
					url = ToFileUrl(candidate);
					return true;
				}
			}

			var data = Application.dataPath;
			if (!string.IsNullOrEmpty(data))
			{
				var candidate = Path.Combine(data, relative);
				if (File.Exists(candidate))
				{
					url = ToFileUrl(candidate);
					return true;
				}
			}

			url = null;
			return false;
		}

		private static string CombineBaseAndRelative(string basePath, string relative)
		{
			if (string.IsNullOrEmpty(basePath))
				return relative;

			var rel = relative.Replace("\\", "/");
			if (LooksLikeUrl(basePath))
				return basePath.TrimEnd('/') + "/" + rel;

			return Path.Combine(basePath, relative);
		}

		private static string ToFileUrl(string path)
		{
			if (string.IsNullOrEmpty(path))
				return path;

			var normalized = path.Replace("\\", "/");
			if (normalized.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
				return normalized;

			return "file://" + normalized;
		}

		private static bool LooksLikeUrl(string value)
		{
			if (string.IsNullOrEmpty(value))
				return false;

			if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
				return true;
			if (value.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
				return true;
			return value.Contains("://", StringComparison.Ordinal);
		}
	}
}
