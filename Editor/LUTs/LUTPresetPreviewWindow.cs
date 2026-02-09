#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PHORIA.Studios.Mandala;

namespace PHORIA.Studios.Mandala.Editor
{

	public class LUTPresetPreviewWindow : EditorWindow
	{
		[SerializeField] private LUTPreset preset;
		[SerializeField] private Texture2D source;

		[Header("Input / Output color handling")]
		[SerializeField] private bool assumeSourceIsSRGB = true; // typical PNG/JPG
		[SerializeField] private bool previewOutputAsSRGB = true; // so the preview looks right in editor UI

		[Header("Performance")]
		[SerializeField] private float downscaleMaxWidth = 1024f;

		[Header("Live Update")]
		[SerializeField] private bool liveUpdate = true;
		[SerializeField] private double liveUpdateIntervalSeconds = 0.20;

		[Header("LUT Preview")]
		[SerializeField] private bool showLutPreview = true;

		[Header("LUT Export")]
		[SerializeField] private bool exportLutAlsoBuildsIfMissing = true;
		[SerializeField] private bool flipVerticalLut = true; // top-left black LUTs (common) -> flip for bottom-left expectations
		[SerializeField] private bool exportImporterSRGB = false; // output LUT texture importer sRGB flag (default OFF)

		private Texture2D _previewOut;
		private Texture2D _lutPreview; // 256 x 16, Standard strip layout

		private Vector2 _scroll;

		private double _nextUpdateTime;
		private int _lastPresetHash;
		private int _lastSettingsHash;
		private bool _dirty;

		[MenuItem("Mandala/LUT Preset Preview (Live)")]
		public static void Open()
		{
			var w = GetWindow<LUTPresetPreviewWindow>("Preset Preview");
			w.minSize = new Vector2(820, 520);
		}

		private void OnEnable()
		{
			EditorApplication.update += OnEditorUpdate;
			Undo.undoRedoPerformed += MarkDirty;
			_dirty = true;
			_nextUpdateTime = 0;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			Undo.undoRedoPerformed -= MarkDirty;

			DestroyImmediate(_previewOut);
			DestroyImmediate(_lutPreview);
		}

		private void MarkDirty()
		{
			_dirty = true;
			_nextUpdateTime = 0;
		}

		private void OnEditorUpdate()
		{
			if (!liveUpdate) return;

			double t = EditorApplication.timeSinceStartup;
			if (t < _nextUpdateTime) return;

			int ph = GetPresetHash(preset);
			int sh = GetSettingsHash();

			bool changed = _dirty || (ph != _lastPresetHash) || (sh != _lastSettingsHash);

			if (!changed) return;

			_dirty = false;
			_lastPresetHash = ph;
			_lastSettingsHash = sh;

			_nextUpdateTime = t + Math.Max(0.05, liveUpdateIntervalSeconds);

			// Update output image (needs preset + source)
			if (preset && source)
				ApplyPresetToPreview();

			// Update LUT preview (needs preset)
			if (preset && showLutPreview)
				BuildLutPreviewTexture();

			Repaint();
		}

		private int GetSettingsHash()
		{
			unchecked
			{
				int h = 17;
				h = h * 31 + (source ? source.GetInstanceID() : 0);
				h = h * 31 + assumeSourceIsSRGB.GetHashCode();
				h = h * 31 + previewOutputAsSRGB.GetHashCode();
				h = h * 31 + downscaleMaxWidth.GetHashCode();
				h = h * 31 + showLutPreview.GetHashCode();
				return h;
			}
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Preset → Preview Image", EditorStyles.boldLabel);
			EditorGUILayout.Space(4);

			using (new EditorGUILayout.VerticalScope("box"))
			{
				EditorGUI.BeginChangeCheck();

				preset = (LUTPreset)EditorGUILayout.ObjectField(
					"Preset (LUTPreset)", preset, typeof(LUTPreset), false);

				source = (Texture2D)EditorGUILayout.ObjectField(
					"Source Image", source, typeof(Texture2D), false);

				EditorGUILayout.Space(6);

				assumeSourceIsSRGB = EditorGUILayout.ToggleLeft("Assume source is sRGB (convert to Linear before grading)", assumeSourceIsSRGB);
				previewOutputAsSRGB = EditorGUILayout.ToggleLeft("Preview output as sRGB", previewOutputAsSRGB);

				downscaleMaxWidth = EditorGUILayout.Slider(new GUIContent("Max preview width"), downscaleMaxWidth, 256f, 4096f);

				EditorGUILayout.Space(8);

				using (new EditorGUILayout.HorizontalScope())
				{
					liveUpdate = EditorGUILayout.ToggleLeft("Live Update", liveUpdate, GUILayout.Width(110));
					EditorGUI.BeginDisabledGroup(!liveUpdate);
					liveUpdateIntervalSeconds = EditorGUILayout.Slider(new GUIContent("Interval (s)"), (float)liveUpdateIntervalSeconds, 0.05f, 1.0f);
					EditorGUI.EndDisabledGroup();
				}

				showLutPreview = EditorGUILayout.ToggleLeft("Show LUT preview (Standard Strip 256×16: x=r+b*16, y=g)", showLutPreview);

				EditorGUILayout.Space(6);
				exportLutAlsoBuildsIfMissing = EditorGUILayout.ToggleLeft("Export builds LUT automatically if missing", exportLutAlsoBuildsIfMissing);
				flipVerticalLut = EditorGUILayout.ToggleLeft("Flip LUT vertically (Y)", flipVerticalLut);
				exportImporterSRGB = EditorGUILayout.ToggleLeft("Export LUT importer sRGB (usually OFF for URP Color Lookup)",exportImporterSRGB);
				EditorGUILayout.Space(8);

				using (new EditorGUILayout.HorizontalScope())
				{
					GUI.enabled = preset && source;
					if (GUILayout.Button("Apply Once", GUILayout.Height(26)))
					{
						ApplyPresetToPreview();
						if (showLutPreview) BuildLutPreviewTexture();
					}
					GUI.enabled = preset && showLutPreview;
					if (GUILayout.Button("Rebuild LUT Preview", GUILayout.Height(26)))
					{
						BuildLutPreviewTexture();
					}

					// Export LUT texture (2D strip) for OVRPassthroughLayer and Unity Post-Process Color Lookup
					GUI.enabled = preset && showLutPreview;
					if (GUILayout.Button("Save LUT as PNG…", GUILayout.Height(26)))
					{
						ExportLutPng();
					}

					GUI.enabled = true;

					if (GUILayout.Button("Clear Output", GUILayout.Height(26)))
					{
						DestroyImmediate(_previewOut);
						_previewOut = null;
						DestroyImmediate(_lutPreview);
						_lutPreview = null;
					}
				}

				if (EditorGUI.EndChangeCheck())
				{
					MarkDirty();
				}

				EditorGUILayout.Space(2);
				EditorGUILayout.HelpBox(
					"If Apply fails: select the source texture asset and enable Read/Write in Import Settings.",
					MessageType.Info);

				EditorGUILayout.Space(2);
				EditorGUILayout.HelpBox(
					"Export produces a 2D LUT strip PNG (256×16, dim=16). Import is forced to Linear (sRGB off), Clamp, no Mips.\n" +
					"Use it for OVRPassthroughLayer LUT and also for URP/HDRP Volume Color Lookup.",
					MessageType.None);
			}

			EditorGUILayout.Space(8);
			DrawPreviews();
		}

		private void DrawPreviews()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			float w = position.width;
			float pad = 10f;

			// Row: Source / After
			float colW = (w - pad * 3f) * 0.5f;
			float colH = Mathf.Max(260f, colW * 0.70f);

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(pad);
				DrawTexBox("Source", source, colW, colH);
				GUILayout.Space(pad);
				DrawTexBox("After (preset applied)", _previewOut, colW, colH);
				GUILayout.Space(pad);
			}

			EditorGUILayout.Space(10);

			if (showLutPreview)
			{
				float lutW = w - pad * 2f;
				float lutH = Mathf.Clamp(lutW * 0.18f, 80f, 180f);

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.Space(pad);
					DrawTexBox("LUT Preview (Standard Strip) — 256×16 (x=r+b*16, y=g)", _lutPreview, lutW, lutH);
					GUILayout.Space(pad);
				}
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawTexBox(string title, Texture2D tex, float width, float height)
		{
			using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
			{
				EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

				Rect r = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
				EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f, 1f));

				if (tex)
				{
					float aspect = (float)tex.width / Mathf.Max(1, tex.height);
					Rect fit = FitRect(r, aspect);
					GUI.DrawTexture(fit, tex, ScaleMode.ScaleToFit, false);
				}
				else
				{
					GUI.Label(r, "(none)", EditorStyles.centeredGreyMiniLabel);
				}
			}
		}

		private static Rect FitRect(Rect outer, float aspect)
		{
			float ow = outer.width;
			float oh = outer.height;

			float targetW = ow;
			float targetH = ow / Mathf.Max(0.0001f, aspect);

			if (targetH > oh)
			{
				targetH = oh;
				targetW = oh * aspect;
			}

			float x = outer.x + (ow - targetW) * 0.5f;
			float y = outer.y + (oh - targetH) * 0.5f;

			return new Rect(x, y, targetW, targetH);
		}

		private void ApplyPresetToPreview()
		{
			if (!preset || !source) return;

			if (!IsReadable(source))
			{
				EditorUtility.DisplayDialog(
					"Texture not readable",
					"Source texture is not readable. Enable Read/Write in the texture import settings.",
					"OK");
				return;
			}

			// Downscale for speed
			Texture2D srcTex = source;
			bool temp = false;

			if (downscaleMaxWidth > 0 && source.width > downscaleMaxWidth)
			{
				float scale = downscaleMaxWidth / source.width;
				int nw = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
				int nh = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
				srcTex = DownscaleNearest(source, nw, nh);
				temp = true;
			}

			var srcPixels = srcTex.GetPixels32();
			var outPixels = new Color32[srcPixels.Length];

			var eval = new LUTPresetEvaluator(preset);

			for (int i = 0; i < srcPixels.Length; i++)
			{
				Color32 c32 = srcPixels[i];
				Color c = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, c32.a / 255f);

				// Interpret source as sRGB -> linear before grading 
				if (assumeSourceIsSRGB)
				{
					c.r = Mathf.GammaToLinearSpace(c.r);
					c.g = Mathf.GammaToLinearSpace(c.g);
					c.b = Mathf.GammaToLinearSpace(c.b);
				}

				// Grade in linear 
				Color g = eval.Apply(c);

				// Convert to sRGB for preview display 
				if (previewOutputAsSRGB)
				{
					g.r = Mathf.LinearToGammaSpace(g.r);
					g.g = Mathf.LinearToGammaSpace(g.g);
					g.b = Mathf.LinearToGammaSpace(g.b);
				}

				outPixels[i] = new Color32(
					(byte)Mathf.RoundToInt(Mathf.Clamp01(g.r) * 255f),
					(byte)Mathf.RoundToInt(Mathf.Clamp01(g.g) * 255f),
					(byte)Mathf.RoundToInt(Mathf.Clamp01(g.b) * 255f),
					(byte)Mathf.RoundToInt(Mathf.Clamp01(g.a) * 255f)
				);
			}

			DestroyImmediate(_previewOut);
			_previewOut = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false, false);
			_previewOut.name = $"{source.name}_AFTER_{preset.name}";
			_previewOut.SetPixels32(outPixels);
			_previewOut.Apply(false, false);

			if (temp) DestroyImmediate(srcTex);
		}

		private void BuildLutPreviewTexture()
		{
			if (!preset) return;

			DestroyImmediate(_lutPreview);
			_lutPreview = LUTGenerator.GenerateLutTexture(preset, flipVertical: flipVerticalLut, linear: false);
		}

		private void ExportLutPng()
		{
			if (!preset) return;

			// Ensure LUT exists
			if ((_lutPreview == null) && exportLutAlsoBuildsIfMissing)
				BuildLutPreviewTexture();

			if (_lutPreview == null)
			{
				EditorUtility.DisplayDialog("No LUT to export", "Build the LUT preview first (or enable auto-build) before exporting.", "OK");
				return;
			}

			// For both OVRPassthroughLayer and Unity Color Lookup:
			// strip format: width = dim*dim, height = dim. Here dim=16 => 256×16.
			Texture2D texToSave = _lutPreview;

			string defaultName = $"LUT_{preset.name}_256x16";
			string savePath = EditorUtility.SaveFilePanelInProject(
				"Save LUT texture (PNG)",
				defaultName,
				"png",
				"Exports a 2D LUT strip (256×16). Use for OVRPassthroughLayer LUT and URP/HDRP Color Lookup."
			);

			if (string.IsNullOrEmpty(savePath))
				return;

			try
			{
				byte[] png = texToSave.EncodeToPNG();
				File.WriteAllBytes(savePath, png);

				AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

				// Make sure it imports as a LUT-friendly texture (Linear, Clamp, no mips).
				ConfigureImportedLutTexture(savePath,exportImporterSRGB);

				EditorUtility.DisplayDialog("LUT exported", $"Saved LUT texture:\n{savePath}", "OK");
			}
			catch (Exception e)
			{
				EditorUtility.DisplayDialog("Export failed", $"Failed to save LUT texture.\n\n{e.Message}", "OK");
			}
		}

		private static void ConfigureImportedLutTexture(string assetPath , bool sRGB)
		{
			var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
			if (importer == null) return;

			importer.textureType = TextureImporterType.Default;
			importer.sRGBTexture = sRGB;
			importer.mipmapEnabled = false;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.filterMode = FilterMode.Bilinear; // Unity Color Lookup expects smooth sampling
			importer.npotScale = TextureImporterNPOTScale.None;
			importer.alphaSource = TextureImporterAlphaSource.FromInput;
			importer.isReadable = true;                // optional, but useful for debug / re-export

			//no compression for OVR passthrough compatibility 
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.crunchedCompression = false;

			//Force Android safe guard 
			var android = importer.GetPlatformTextureSettings("Android");
			android.overridden = true;
			android.format = TextureImporterFormat.RGB24;   
			android.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SetPlatformTextureSettings(android);

			importer.SaveAndReimport();
		}


		private static bool IsReadable(Texture2D tex)
		{
			string path = AssetDatabase.GetAssetPath(tex);
			if (string.IsNullOrEmpty(path)) return true;
			var importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null) return true;
			return importer.isReadable;
		}

		private static Texture2D DownscaleNearest(Texture2D src, int w, int h)
		{
			var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
			dst.name = src.name + "_DS";

			var sp = src.GetPixels32();
			var dp = new Color32[w * h];

			for (int y = 0; y < h; y++)
			{
				int sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)h) * src.height), 0, src.height - 1);
				for (int x = 0; x < w; x++)
				{
					int sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)w) * src.width), 0, src.width - 1);
					dp[y * w + x] = sp[sy * src.width + sx];
				}
			}

			dst.SetPixels32(dp);
			dst.Apply(false, false);
			return dst;
		}

		private static int GetPresetHash(LUTPreset p)
		{
			if (!p) return 0;
			unchecked
			{
				int h = 17;
				h = h * 31 + p.GetInstanceID();

				h = h * 31 + p.shadowEnd.GetHashCode();
				h = h * 31 + p.highlightStart.GetHashCode();
				h = h * 31 + p.strength.GetHashCode();
				h = h * 31 + p.contrast.GetHashCode();
				h = h * 31 + p.pivot.GetHashCode();
				h = h * 31 + p.saturation.GetHashCode();
				h = h * 31 + p.exposure.GetHashCode();
				h = h * 31 + p.gamma.GetHashCode();

				h = h * 31 + p.shadowTint.GetHashCode();
				h = h * 31 + p.midTint.GetHashCode();
				h = h * 31 + p.highlightTint.GetHashCode();

				return h;
			}
		}

		// ---- Driver-matching preset math (linear) ----
		private struct LUTPresetEvaluator
		{
			private readonly float shadowEnd;
			private readonly float highlightStart;
			private readonly float feather;

			private readonly float strength;
			private readonly float contrast;
			private readonly float pivot;
			private readonly float saturation;
			private readonly float exposure;
			private readonly float invGamma;

			private readonly Vector3 shadowTint;
			private readonly Vector3 midTint;
			private readonly Vector3 highlightTint;

			public LUTPresetEvaluator(LUTPreset p)
			{
				float se = Mathf.Clamp01(p.shadowEnd);
				float hs = Mathf.Clamp01(p.highlightStart);
				if (hs < se) { float tmp = hs; hs = se; se = tmp; }

				shadowEnd = se;
				highlightStart = hs;

				float span = Mathf.Max(0.0001f, highlightStart - shadowEnd);
				feather = Mathf.Clamp(span * 0.25f, 0.02f, 0.12f);

				strength = Mathf.Clamp01(p.strength);
				contrast = Mathf.Max(0.0001f, p.contrast);
				pivot = Mathf.Clamp01(p.pivot);
				saturation = Mathf.Max(0f, p.saturation);
				exposure = p.exposure;

				float gamma = Mathf.Max(0.0001f, p.gamma);
				invGamma = 1f / gamma;

				// LUTobject tints are Linear
				Color st = p.shadowTint; st.a = 1f;
				Color mt = p.midTint; mt.a = 1f;
				Color ht = p.highlightTint; ht.a = 1f;

				shadowTint = new Vector3(st.r, st.g, st.b);
				midTint = new Vector3(mt.r, mt.g, mt.b);
				highlightTint = new Vector3(ht.r, ht.g, ht.b);
			}

			public Color Apply(Color cLinear)
			{
				Vector3 c = new Vector3(cLinear.r, cLinear.g, cLinear.b);

				float lum = c.x * 0.2126f + c.y * 0.7152f + c.z * 0.0722f;

				float wShadow = 1f - Smoothstep(shadowEnd - feather, shadowEnd + feather, lum);
				float wHighlight = Smoothstep(highlightStart - feather, highlightStart + feather, lum);
				float wMid = Mathf.Clamp01(1f - wShadow - wHighlight);

				Vector3 tint = shadowTint * wShadow + midTint * wMid + highlightTint * wHighlight;

				Vector3 graded = new Vector3(c.x * tint.x, c.y * tint.y, c.z * tint.z);
				graded += new Vector3(exposure, exposure, exposure);

				Vector3 pv = new Vector3(pivot, pivot, pivot);
				graded = (graded - pv) * contrast + pv;

				float lum2 = graded.x * 0.2126f + graded.y * 0.7152f + graded.z * 0.0722f;
				Vector3 grey = new Vector3(lum2, lum2, lum2);
				graded = Vector3.Lerp(grey, graded, saturation);

				graded.x = Mathf.Pow(Mathf.Max(0f, graded.x), invGamma);
				graded.y = Mathf.Pow(Mathf.Max(0f, graded.y), invGamma);
				graded.z = Mathf.Pow(Mathf.Max(0f, graded.z), invGamma);

				Vector3 outC = Vector3.Lerp(c, graded, strength);

				return new Color(
					Mathf.Clamp01(outC.x),
					Mathf.Clamp01(outC.y),
					Mathf.Clamp01(outC.z),
					cLinear.a
				);
			}

			private static float Smoothstep(float a, float b, float x)
			{
				float t = Mathf.InverseLerp(a, b, x);
				return t * t * (3f - 2f * t);
			}
		}

		// ---- LUT generation (Standard strip ordering: g outer, b middle, r inner) ----
		// Remove the private BuildPresetLut method entirely - now using LUTGenerator

		// Remove the private Smoothstep method at the end - now in LUTGenerator

		// Keep PresetEvaluator struct for the image preview functionality
		// ...existing code for PresetEvaluator struct...
	}
}
#endif
