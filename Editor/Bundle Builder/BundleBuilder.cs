using UnityEngine;

namespace PHORIA.Mandala.SDK.Editor
{
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Reflection; // Required for System.FlagsAttribute

public class BundleBuilderWindow : EditorWindow
{
    // --- Configuration Variables ---
    private BuildTarget _selectedBuildTarget = BuildTarget.StandaloneWindows; // Default to Windows
    private BuildAssetBundleOptions _selectedBuildOptions = BuildAssetBundleOptions.None;
    private string _outputPath = "AssetBundles"; // Default output path relative to project root

    // --- UI State Variables ---
    private Vector2 _scrollPosition;

    // --- Menu Item to Open the Window ---
    [MenuItem("PHORIA/Mandala SDK/Bundle Builder")]
    public static void ShowWindow()
    {
        // Get existing open window or create a new one
        BundleBuilderWindow window = GetWindow<BundleBuilderWindow>("Bundle Builder");
        window.minSize = new Vector2(500, 600); // Set a minimum size for the window
        window.Show();
    }

    // --- OnGUI is called when the GUI is rendered ---
    void OnGUI()
    {
        // --- Window Title ---
        EditorGUILayout.LabelField("Comprehensive AssetBundle Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // --- Output Path Selection ---
        EditorGUILayout.LabelField("Output Path", EditorStyles.largeLabel);
        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Build Directory", _outputPath);
        if (GUILayout.Button("Browse...", GUILayout.Width(100)))
        {
            string newPath = EditorUtility.OpenFolderPanel("Select AssetBundle Output Directory", _outputPath, "");
            
            if (!string.IsNullOrEmpty(newPath))
            {
	            // Make path relative to project if possible, otherwise keep absolute
                string projectPath = Application.dataPath.Replace("/Assets", "");
                
                _outputPath = newPath.StartsWith(projectPath) ? newPath[(projectPath.Length + 1)..] : newPath;
            }
        }
        
        // _outputPath = Path.Combine(_outputPath, _selectedBuildTarget.ToString());

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("AssetBundles will be saved in:\n" + Path.Combine(Path.GetFullPath(_outputPath), _selectedBuildTarget.ToString()), MessageType.Info);
        EditorGUILayout.Space();

        // --- Platform Selection ---
        EditorGUILayout.LabelField("Target Platform", EditorStyles.largeLabel);
        _selectedBuildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Build For", _selectedBuildTarget);
        EditorGUILayout.Space();

        // --- BuildAssetBundleOptions Selection ---
        EditorGUILayout.LabelField("Build Options (BuildAssetBundleOptions)", EditorStyles.largeLabel);
        EditorGUILayout.HelpBox("Select the desired flags for your AssetBundle build. These are bitwise options.", MessageType.None);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        // Get all values of the BuildAssetBundleOptions enum
        // We use reflection to ensure we capture all defined flags,
        // even if new ones are added in future Unity versions.
        foreach (BuildAssetBundleOptions option in Enum.GetValues(typeof(BuildAssetBundleOptions)))
        {
            // 'None' is a special case, it shouldn't be a toggle itself,
            // but represents no options selected.
            if (option == BuildAssetBundleOptions.None)
                continue;

            // Check if the current option is a power of two (i.e., a single flag)
            // and not a combination of flags (like 'All' if it existed).
            // This prevents creating toggles for composite values if they were defined.
            if ((option & (option - 1)) == 0) // Check if it's a power of 2
            {
                // Get the tooltip text for the current option
                string tooltip = GetOptionTooltip(option);

                bool isOptionEnabled = (_selectedBuildOptions & option) == option;
                // Use GUIContent to add the tooltip
                bool newOptionEnabled = EditorGUILayout.Toggle(new GUIContent(option.ToString(), tooltip), isOptionEnabled);

                if (newOptionEnabled && !isOptionEnabled)
                {
                    _selectedBuildOptions |= option; // Add the flag
                }
                else if (!newOptionEnabled && isOptionEnabled)
                {
                    _selectedBuildOptions &= ~option; // Remove the flag
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();

        // --- Build Button ---
        if (GUILayout.Button("Build AssetBundles", GUILayout.Height(40)))
        {
            BuildAssetBundles();
        }
    }

    // --- Core Build Logic ---
    private void BuildAssetBundles()
    {
        // Ensure the output directory exists
        string fullOutputPath = Path.GetFullPath(_outputPath);
        fullOutputPath = Path.Combine(fullOutputPath, _selectedBuildTarget.ToString());
        if (!Directory.Exists(fullOutputPath))
        {
            Directory.CreateDirectory(fullOutputPath);
            Debug.Log($"Created output directory: {fullOutputPath}");
        }

        Debug.Log($"Starting AssetBundle build for platform: {_selectedBuildTarget} with options: {_selectedBuildOptions}");
        Debug.Log($"Output path: {fullOutputPath}");

        AssetBundleBuild[] builds = GetAssetBundleBuilds();

        if (builds.Length == 0)
        {
            Debug.LogWarning("No AssetBundles are defined in your project. Please define AssetBundles in the Inspector for your assets.");
            EditorUtility.DisplayDialog("Build Warning", "No AssetBundles are defined in your project. Please define AssetBundles in the Inspector for your assets (select an asset and assign it an AssetBundle name).", "OK");
            return;
        }

        // Perform the build
        try
        {
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(fullOutputPath, builds, _selectedBuildOptions, _selectedBuildTarget);

            if (manifest == null)
            {
                Debug.LogError("AssetBundle build failed! Check console for errors.");
                EditorUtility.DisplayDialog("Build Failed", "AssetBundle build process returned null manifest. Check Unity console for detailed errors.", "OK");
            }
            else
            {
                Debug.Log("AssetBundle build completed successfully!");
                EditorUtility.DisplayDialog("Build Complete", "AssetBundles built successfully to:\n" + fullOutputPath, "OK");
                // You can add further processing here, e.g., opening the folder
                // EditorUtility.RevealInFinder(fullOutputPath); // Not always reliable cross-platform
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"An error occurred during AssetBundle build: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("Build Error", $"An unexpected error occurred during build: {e.Message}", "OK");
        }
    }

    // --- Helper to get defined AssetBundles ---
    private AssetBundleBuild[] GetAssetBundleBuilds()
    {
        // This method collects all unique AssetBundle names defined in the project.
        // For a more advanced builder, you might want to allow users to select
        // which specific AssetBundles to build, or group them.
        string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
        AssetBundleBuild[] builds = new AssetBundleBuild[assetBundleNames.Length];

        for (int i = 0; i < assetBundleNames.Length; i++)
        {
            string bundleName = assetBundleNames[i];
            string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);

            if (assetPaths.Length > 0)
            {
                builds[i].assetBundleName = bundleName;
                builds[i].assetNames = assetPaths;
                Debug.Log($"Found AssetBundle: {bundleName} with {assetPaths.Length} assets.");
            }
            else
            {
                Debug.LogWarning($"AssetBundle '{bundleName}' has no assets assigned to it. It will be skipped.");
            }
        }
        return builds;
    }

    // --- Helper to provide tooltips for BuildAssetBundleOptions ---
    private string GetOptionTooltip(BuildAssetBundleOptions option) =>
	    option switch
	    {
		    BuildAssetBundleOptions.UncompressedAssetBundle =>
			    "Builds AssetBundles without any compression. This results in larger file sizes but provides the fastest loading times at runtime, as no decompression is needed.",
		    // BuildAssetBundleOptions.CollectDependencies =>
			    // "DEPRECATED: This option has been made obsolete. It is always enabled in the new AssetBundle build system introduced in Unity 5.0, meaning all dependencies are automatically collected.",
		    // BuildAssetBundleOptions.CompleteAssets =>
			    // "DEPRECATED: This option has been made obsolete. It is always disabled in the new AssetBundle build system introduced in Unity 5.0, meaning assets are not forced to include their entire content if parts are not used.",
		    BuildAssetBundleOptions.DisableWriteTypeTree =>
			    "Excludes type information (metadata) for the assets from the AssetBundle. This makes the bundle smaller but may reduce forward compatibility with future Unity versions if asset types change significantly.",
		    // BuildAssetBundleOptions.DeterministicAssetBundle =>
			    // "Ensures that the AssetBundle build is deterministic. If the same assets are built with the same options, the resulting AssetBundle will be byte-for-byte identical. This is crucial for caching and incremental build systems. This feature is now always enabled.",
		    BuildAssetBundleOptions.ForceRebuildAssetBundle =>
			    "Forces a complete rebuild of all AssetBundles, ignoring any existing cached data. Use this when you want to ensure all assets are reprocessed from scratch, for example, after a Unity version upgrade or when troubleshooting build issues.",
		    BuildAssetBundleOptions.IgnoreTypeTreeChanges =>
			    "When building, Unity will ignore changes to the type tree (metadata) of assets. This can prevent unnecessary rebuilds if only the type tree has changed, but the actual asset data remains the same, optimizing incremental builds.",
		    BuildAssetBundleOptions.AppendHashToAssetBundleName =>
			    "Appends a content-based hash to the AssetBundle name (e.g., mybundle_abcdef123456.bundle). This is highly recommended for versioning and cache busting, as a new hash indicates a new version of the bundle, ensuring clients download the latest content.",
		    BuildAssetBundleOptions.ChunkBasedCompression =>
			    "Uses LZ4 compression, which is a chunk-based compression algorithm. This generally provides faster loading times compared to LZMA, as only necessary chunks are decompressed, and it supports incremental updates to bundles more efficiently.",
		    BuildAssetBundleOptions.StrictMode =>
			    "Aborts the AssetBundle build process immediately if any errors are reported during it. This ensures that only perfectly built bundles are produced, preventing the deployment of potentially broken AssetBundles.",
		    BuildAssetBundleOptions.DryRunBuild =>
			    "Performs a 'dry run' of the build process. It simulates the build, checks for errors and dependencies, but does not actually write any AssetBundle files to disk. This is useful for validating your AssetBundle setup without generating output files.",
		    BuildAssetBundleOptions.DisableLoadAssetByFileName =>
			    "Disables the ability to load assets from within this AssetBundle using only their file name (e.g., 'myasset.prefab'). Assets can only be loaded by their full asset path (e.g., 'Assets/Prefabs/myasset.prefab'), which can help prevent naming conflicts.",
		    BuildAssetBundleOptions.DisableLoadAssetByFileNameWithExtension =>
			    "Disables the ability to load assets from within this AssetBundle using their file name including its extension (e.g., 'myasset.prefab'). Assets can only be loaded by their full asset path, similar to DisableLoadAssetByFileName.",
		    BuildAssetBundleOptions.AssetBundleStripUnityVersion =>
			    "Removes the Unity Version number from the Archive File and Serialized File headers during the build. This can be useful for reducing file size slightly and obscuring the Unity version used for the build.",
		    BuildAssetBundleOptions.UseContentHash =>
			    "Uses the content of the asset bundle to calculate the hash. This feature is always enabled in current Unity versions, meaning the hash appended to the bundle name (if AppendHashToAssetBundleName is used) will always reflect the bundle's content.",
		    BuildAssetBundleOptions.RecurseDependencies =>
			    "This flag ensures that all dependencies of assets included in the AssetBundle are also included within the same AssetBundle. This can simplify dependency management but might lead to larger bundles if dependencies are shared across many bundles.",
		    BuildAssetBundleOptions.StripUnatlasedSpriteCopies =>
			    "Used to prevent duplicating a texture when it is referenced in multiple bundles, primarily with particle systems. The new behavior does not duplicate the texture if the sprite does not belong to an atlas. Using this flag is the desired behavior, but is not set by default for backwards compatibility reasons.",
		    _ => "No specific description available for this option."
	    };

}
}
