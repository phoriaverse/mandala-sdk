using UnityEngine;

namespace PHORIA.Studios.Mandala
{
	[CreateAssetMenu(menuName = "Mandala/Passthrough LUT Preset", fileName = "PT_LutPreset")]
	public class LUTPreset : ScriptableObject
	{
		[Header("Split Toning (Linear)")]
		public Color shadowTint = new Color(0.90f, 1.00f, 1.10f, 1f);    
		public Color midTint = new Color(1.00f, 1.00f, 1.00f, 1f);    
		public Color highlightTint = new Color(1.10f, 1.00f, 0.90f, 1f); 

		[Header("Split Ranges")]
		[Range(0f, 1f)] public float shadowEnd = 0.35f;
		[Range(0f, 1f)] public float highlightStart = 0.55f;

		[Header("Grade Controls")]
		[Range(0f, 2f)] public float strength = 1.0f;       // global intensity
		[Range(0.1f, 3f)] public float contrast = 1.05f;     
		[Range(0f, 1f)] public float pivot = 0.5f;           // contrast pivot
		[Range(0f, 2f)] public float saturation = 1.05f;     
		[Range(-0.25f, 0.25f)] public float exposure = 0.0f; // simple offset in linear

		[Header("Optional: Gamma")]
		[Range(0.5f, 2.0f)] public float gamma = 1.0f;      
	}
}
