#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using PHORIA.Studios.Mandala;

namespace PHORIA.Studios.Mandala.Editor
{
    public class LUTAutoMatcherWindow : EditorWindow
    {
        [SerializeField] private Texture2D sourceImage;
        [SerializeField] private string presetName = "NewLUTPreset";
        [SerializeField] private string saveFolderPath = "Assets/Project/Content/LUTPresets";

        [MenuItem("Mandala/LUT Auto Matcher")]
        public static void Open()
        {
            var window = GetWindow<LUTAutoMatcherWindow>("LUT Auto Matcher");
            window.minSize = new Vector2(400, 0);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Extract LUT Preset from Image", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Source Image", EditorStyles.miniBoldLabel);
                sourceImage = (Texture2D)EditorGUILayout.ObjectField(
                    "Image", sourceImage, typeof(Texture2D), false);

                EditorGUILayout.Space(8);

                EditorGUILayout.LabelField("Output Settings", EditorStyles.miniBoldLabel);
                presetName = EditorGUILayout.TextField("Preset Name", presetName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    saveFolderPath = EditorGUILayout.TextField("Save Folder", saveFolderPath);
                    if (GUILayout.Button("Browse", GUILayout.Width(60)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            // Convert absolute path to relative Assets path
                            if (selected.StartsWith(Application.dataPath))
                                saveFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        }
                    }
                }

                EditorGUILayout.Space(12);

                GUI.enabled = sourceImage != null && !string.IsNullOrWhiteSpace(presetName);
                if (GUILayout.Button("✨ Extract & Create LUT Preset", GUILayout.Height(32)))
                {
                    CreateLUTPresetFromImage();
                }
                GUI.enabled = true;

                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "This will analyze the source image and create a new LUTobject preset " +
                    "with tints, saturation, and exposure values extracted from the image's color characteristics.",
                    MessageType.Info);
            }
        }

        private void CreateLUTPresetFromImage()
        {
            if (sourceImage == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a source image first.", "OK");
                return;
            }

            // Ensure save folder exists
            if (!AssetDatabase.IsValidFolder(saveFolderPath))
            {
                CreateFolderRecursively(saveFolderPath);
            }

            // Create new LUTPreset
            LUTPreset newPreset = ScriptableObject.CreateInstance<LUTPreset>();

            // Extract values from image
            LUTAutoMatcher.ExtractFromTexture(sourceImage, newPreset);

            // Generate unique asset path
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(saveFolderPath, $"{presetName}.asset"));

            // Save asset
            AssetDatabase.CreateAsset(newPreset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the new asset
            Selection.activeObject = newPreset;
            EditorGUIUtility.PingObject(newPreset);

            EditorUtility.DisplayDialog("Success",
                $"LUT Preset created successfully!\n\nSaved to:\n{assetPath}",
                "OK");

            // Update preset name for next creation
            presetName = $"LUT_{sourceImage.name}";
        }

        private static void CreateFolderRecursively(string path)
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0]; // "Assets"

            for (int i = 1; i < folders.Length; i++)
            {
                string parentPath = currentPath;
                currentPath = Path.Combine(currentPath, folders[i]);

                if (!AssetDatabase.IsValidFolder(currentPath))
                {
                    AssetDatabase.CreateFolder(parentPath, folders[i]);
                }
            }
        }
    }
}
#endif
