using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SetKaiser : EditorWindow
{
    private List<string> texturePaths = new List<string>();
    private List<string> allTexturePaths = new List<string>(); // Store all textures before filtering
    private bool scanCompleted = false;
    private Vector2 scrollPosition;
    private bool setNormalMapsToBox = true; // Default to true for Box filter for normal maps
    private bool showAlreadyKaiserTextures = false; // Show textures already using Kaiser filter
    private HashSet<string> selectedPaths = new HashSet<string>();
    private double lastClickTime = 0;
    private const double doubleClickTime = 0.3;
    private string lastSelectedPath = null; // Track last selected path for shift-select
    private bool isDragging = false; // Track if we're currently dragging
    private int dragStartIndex = -1; // The index where the drag started
    private int focusedIndex = -1; // Current focused/selected index for keyboard navigation
    
    // New settings for auto functionality
    private bool autoRescan = false; // Auto rescan when Unity recompiles or window is focused
    private bool autoApply = false; // Auto apply Kaiser filter even if GUI is closed
    
    // New setting for mip streaming
    private bool enableMipStreaming = true; // Default to true for better performance
    
    // EditorPrefs keys for saving settings
    private const string AUTO_RESCAN_KEY = "SetKaiser_AutoRescan";
    private const string AUTO_APPLY_KEY = "SetKaiser_AutoApply";
    private const string SET_NORMAL_MAPS_TO_BOX_KEY = "SetKaiser_SetNormalMapsToBox";
    private const string ENABLE_MIP_STREAMING_KEY = "SetKaiser_EnableMipStreaming";
    
    // Button styles
    private GUIStyle actionButtonStyle;
    private GUIStyle secondaryButtonStyle;

    // Add static instance accessor to enable communication with post processor
    private static SetKaiser instance;

    [MenuItem("Tools/Set Kaiser Mipmap Filtering")]
    public static void ShowWindow()
    {
        GetWindow<SetKaiser>("Set Kaiser Mipmap Filtering");
    }
    
    // Add OnEnable method to initialize window state
    private void OnEnable()
    {
        // Register this instance for asset change detection
        instance = this;
        
        // Load saved settings
        autoRescan = EditorPrefs.GetBool(AUTO_RESCAN_KEY, false);
        autoApply = EditorPrefs.GetBool(AUTO_APPLY_KEY, false);
        setNormalMapsToBox = EditorPrefs.GetBool(SET_NORMAL_MAPS_TO_BOX_KEY, true);
        enableMipStreaming = EditorPrefs.GetBool(ENABLE_MIP_STREAMING_KEY, true);
        
        // Always reset showAlreadyKaiserTextures to false when opening
        showAlreadyKaiserTextures = false;
        
        // Initialize scanCompleted to true so GUI is visible immediately
        scanCompleted = true;
        
        // Register for asset change events when window is enabled
        EditorApplication.projectChanged += OnProjectChanged;
        
        // Auto scan if the setting is enabled
        if (autoRescan)
        {
            ScanTextures();
        }
    }
    
    private void OnDisable()
    {
        // Unregister from event when window is closed
        EditorApplication.projectChanged -= OnProjectChanged;
        
        // If this instance is the current one, clear it
        if (instance == this)
        {
            instance = null;
        }
    }
    
    // New method to handle project asset changes
    private void OnProjectChanged()
    {
        if (autoRescan && scanCompleted)
        {
            // Trigger a scan but delay it slightly to avoid scanning during import process
            EditorApplication.delayCall += () => {
                if (this != null) {
                    ScanTextures();
                    Repaint();
                }
            };
        }
    }
    
    // Create static accessor for the settings
    public static bool AutoApplyEnabled => EditorPrefs.GetBool("SetKaiser_AutoApply", false);
    public static bool UseBoxForNormalMaps => EditorPrefs.GetBool("SetKaiser_SetNormalMapsToBox", true);
    public static bool EnableMipStreaming => EditorPrefs.GetBool("SetKaiser_EnableMipStreaming", true);
    
    // Method to request a rescan from outside classes
    public static void RequestRescan()
    {
        if (instance != null && instance.autoRescan)
        {
            EditorApplication.delayCall += () => {
                if (instance != null) {
                    instance.ScanTextures();
                    instance.Repaint();
                }
            };
        }
    }
    
    private void OnFocus()
    {
        // Auto rescan when the window regains focus if setting is enabled
        if (autoRescan && scanCompleted)
        {
            ScanTextures();
        }
    }

    private void OnGUI()
    {
        // Initialize button styles
        if (actionButtonStyle == null)
        {
            actionButtonStyle = new GUIStyle(GUI.skin.button);
            actionButtonStyle.fontStyle = FontStyle.Bold;
            actionButtonStyle.fontSize = 12;
            actionButtonStyle.normal.textColor = Color.white;
            actionButtonStyle.hover.textColor = Color.white;
            actionButtonStyle.padding = new RectOffset(12, 12, 8, 8);
            actionButtonStyle.margin = new RectOffset(30, 30, 10, 10);
            actionButtonStyle.alignment = TextAnchor.MiddleCenter;
            
            // Create background textures with blue color for active button
            Texture2D normalBg = new Texture2D(1, 1);
            normalBg.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.9f)); // Blue color
            normalBg.Apply();
            
            Texture2D hoverBg = new Texture2D(1, 1);
            hoverBg.SetPixel(0, 0, new Color(0.3f, 0.5f, 1.0f)); // Lighter blue for hover
            hoverBg.Apply();
            
            actionButtonStyle.normal.background = normalBg;
            actionButtonStyle.hover.background = hoverBg;
            actionButtonStyle.active.background = normalBg;
            
            // Create secondary button style with gray colors for inactive state
            secondaryButtonStyle = new GUIStyle(actionButtonStyle);
            
            Texture2D secondaryNormalBg = new Texture2D(1, 1);
            secondaryNormalBg.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f));
            secondaryNormalBg.Apply();
            
            Texture2D secondaryHoverBg = new Texture2D(1, 1);
            secondaryHoverBg.SetPixel(0, 0, new Color(0.6f, 0.6f, 0.6f));
            secondaryHoverBg.Apply();
            
            secondaryButtonStyle.normal.background = secondaryNormalBg;
            secondaryButtonStyle.hover.background = secondaryHoverBg;
            secondaryButtonStyle.active.background = secondaryNormalBg;
        }
        
        // Set the background color to the default Unity color
        if (GUI.skin.window.normal.background != null && GUI.skin.window.normal.background.isReadable)
        {
            GUI.backgroundColor = GUI.skin.window.normal.background.GetPixel(0, 0);
        }
        else
        {
            GUI.backgroundColor = Color.gray;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Scan Textures", GUILayout.Height(25), GUILayout.Width(350)))
        {
            ScanTextures();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);

        if (scanCompleted)
        {
            // Create a centered title style
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            // Auto-functionality settings section
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Center the title
            EditorGUILayout.LabelField("Automation Settings", titleStyle);
            EditorGUILayout.Space(5);
            
            // Create a toggle style that's centered
            GUIStyle centeredToggle = new GUIStyle(EditorStyles.toggle);
            centeredToggle.alignment = TextAnchor.MiddleCenter;
            
            // Auto Rescan checkbox centered
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            autoRescan = EditorGUILayout.ToggleLeft(
                new GUIContent("Auto Rescan", "Automatically rescan textures when Unity recompiles or window gains focus"), 
                autoRescan, GUILayout.Width(250));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(AUTO_RESCAN_KEY, autoRescan);
            }
            
            // Auto Apply checkbox centered
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            autoApply = EditorGUILayout.ToggleLeft(
                new GUIContent("Auto Apply When Importing", "Automatically apply Kaiser filter to newly imported textures even if this window is closed"), 
                autoApply, GUILayout.Width(250));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(AUTO_APPLY_KEY, autoApply);
                
                if (autoApply)
                {
                    Debug.Log("Auto-apply Kaiser filter enabled. Newly imported textures will be processed automatically.");
                }
                else
                {
                    Debug.Log("Auto-apply Kaiser filter disabled.");
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
            
            // Texture optimization settings box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            // Center the title
            EditorGUILayout.LabelField("Texture Optimization Settings", titleStyle);
            EditorGUILayout.Space(5);
            
            // Normal maps to Box filter checkbox centered
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIContent normalMapToggleContent = new GUIContent(
                "Set Normal Maps to Box Filter (Recommended)", 
                "Normal maps contain directional data that can be distorted by Kaiser filtering, causing visual artifacts. Box filter preserves the perpendicular relationship between XYZ components in normal maps, resulting in more accurate lighting and fewer artifacts."
            );
            setNormalMapsToBox = EditorGUILayout.ToggleLeft(normalMapToggleContent, setNormalMapsToBox, 
                GUILayout.Width(350));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // Mip streaming checkbox centered
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIContent mipStreamingContent = new GUIContent(
                "Enable Mip Streaming (Recommended)", 
                "Mip Streaming loads lower-resolution mipmaps first and streams in higher detail as needed. This improves performance and reduces memory usage, especially for VR applications."
            );
            enableMipStreaming = EditorGUILayout.ToggleLeft(mipStreamingContent, enableMipStreaming, 
                GUILayout.Width(350));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
            
            // Format the "Show Already Processed Textures" checkbox to match other UI elements
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Use the centered style for the checkbox
            bool previousValue = showAlreadyKaiserTextures;
            showAlreadyKaiserTextures = EditorGUILayout.ToggleLeft(
                "Show Already Processed Textures", 
                showAlreadyKaiserTextures,
                GUILayout.Width(250));
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
            
            // If the checkbox value changed, refilter the texture list
            if (previousValue != showAlreadyKaiserTextures)
            {
                FilterTextures();
                // Clear selection when filter changes
                selectedPaths.Clear();
                lastSelectedPath = null;
                focusedIndex = -1;
            }

            // Add top bar for columns with lines
            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // Top line
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preview", EditorStyles.boldLabel, GUILayout.Width(40));
            GUILayout.Box("", GUILayout.Width(1), GUILayout.Height(20)); // Vertical line
            GUILayout.Label("Name", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            GUILayout.Box("", GUILayout.Width(1), GUILayout.Height(20)); // Vertical line
            GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Box("", GUILayout.Width(1), GUILayout.Height(20)); // Vertical line
            GUILayout.Label("Filtering", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Box("", GUILayout.Width(1), GUILayout.Height(20)); // Vertical line
            GUILayout.Label("Streaming", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // Bottom line
            GUILayout.Space(5);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            
            // Handle keyboard events for navigation
            HandleKeyboardNavigation();
            
            // Handle mouse up event to end dragging
            if (Event.current.type == EventType.MouseUp && isDragging)
            {
                isDragging = false;
                Event.current.Use();
            }
            
            bool isLighter = false; // Initialize the variable that was missing
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string path = texturePaths[i];
                isLighter = !isLighter;
                GUIStyle rowStyle = new GUIStyle();
                rowStyle.normal.background = new Texture2D(1, 1);
                rowStyle.normal.background.SetPixel(0, 0, isLighter ? GUI.backgroundColor : new Color(0.7f, 0.7f, 0.7f, 1f)); // Default color or slightly darker
                rowStyle.normal.background.Apply();

                if (selectedPaths.Contains(path))
                {
                    rowStyle.normal.background = new Texture2D(1, 1);
                    rowStyle.normal.background.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.8f, 1f)); // Blue color
                    rowStyle.normal.background.Apply();
                }

                GUILayout.BeginHorizontal(rowStyle);
                
                // Create styles for vertically centered text
                GUIStyle verticalCenterStyle = new GUIStyle();
                verticalCenterStyle.normal.textColor = Color.white;
                verticalCenterStyle.alignment = TextAnchor.MiddleLeft; // Vertically center and left align
                verticalCenterStyle.fontSize = GUI.skin.label.fontSize;
                
                GUIStyle centerStyle = new GUIStyle();
                centerStyle.normal.textColor = Color.white;
                centerStyle.alignment = TextAnchor.MiddleCenter; // Center both vertically and horizontally
                centerStyle.fontSize = GUI.skin.label.fontSize;

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                
                // Display texture preview
                if (texture != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(texture);
                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    
                    // Create content for tooltip preview
                    GUIContent previewContent = new GUIContent();
                    previewContent.tooltip = " "; // Set a space to enable tooltips
                    
                    // Check if the texture is a normal map
                    if (importer != null && importer.textureType == TextureImporterType.NormalMap)
                    {
                        // Use AssetPreview to display normal maps correctly
                        Texture2D preview = AssetPreview.GetAssetPreview(texture);
                        if (preview != null)
                        {
                            previewContent.image = preview;
                            GUILayout.Label(previewContent, GUILayout.Width(40), GUILayout.Height(40));
                        }
                        else
                        {
                            // Fallback: Use the mini thumbnail which often shows correct colors
                            Texture2D thumbnail = AssetPreview.GetMiniThumbnail(texture);
                            if (thumbnail != null)
                            {
                                previewContent.image = thumbnail;
                                GUILayout.Label(previewContent, GUILayout.Width(40), GUILayout.Height(40));
                            }
                            else
                            {
                                // Last resort: standard display
                                previewContent.image = texture;
                                GUILayout.Label(previewContent, GUILayout.Width(40), GUILayout.Height(40));
                            }
                        }
                    }
                    else
                    {
                        // For non-normal maps, use the default texture display with tooltip
                        previewContent.image = texture;
                        GUILayout.Label(previewContent, GUILayout.Width(40), GUILayout.Height(40));
                    }
                    
                    // Draw large preview tooltip if mouse is over the image
                    DrawLargePreviewTooltip(texture);
                }
                else
                {
                    GUILayout.Label("N/A", GUILayout.Width(50), GUILayout.Height(40));
                }

                // Display texture name with vertical centering
                GUILayout.Label(System.IO.Path.GetFileName(path), verticalCenterStyle, GUILayout.ExpandWidth(true), GUILayout.Height(40));

                // Display texture format/type instead of importer type
                TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (textureImporter != null)
                {
                    // Get actual texture format information
                    string formatInfo = GetTextureFormatInfo(texture, textureImporter);
                    GUILayout.Label(formatInfo, centerStyle, GUILayout.Width(70), GUILayout.Height(40));
                    GUILayout.Box("", GUILayout.Width(1), GUILayout.Height(40)); // Match the height of the row
                    
                    // Display simplified filter name without the "Filter" suffix
                    string filterName = GetSimplifiedFilterName(textureImporter.mipmapFilter);
                    GUILayout.Label(filterName, centerStyle, GUILayout.Width(70), GUILayout.Height(40));
                    
                    // Add vertical separator line
                    GUILayout.Box("", GUILayout.Width(1), GUILayout.Height(40));
                    
                    // Display mip streaming status with color coded text
                    GUIStyle streamingStyle = new GUIStyle(centerStyle);
                    if (textureImporter.streamingMipmaps)
                    {
                        streamingStyle.normal.textColor = new Color(0.4f, 0.8f, 0.4f); // Green for enabled
                        GUILayout.Label("Enabled", streamingStyle, GUILayout.Width(70), GUILayout.Height(40));
                    }
                    else
                    {
                        streamingStyle.normal.textColor = new Color(0.8f, 0.4f, 0.4f); // Red for disabled
                        GUILayout.Label("Disabled", streamingStyle, GUILayout.Width(70), GUILayout.Height(40));
                    }
                }

                GUILayout.EndHorizontal();

                // Make the entire row clickable
                Rect lastRect = GUILayoutUtility.GetLastRect();
                
                // Check if this row is being dragged over
                if (isDragging && Event.current.type == EventType.MouseDrag && lastRect.Contains(Event.current.mousePosition))
                {
                    // Select all items between dragStartIndex and current index
                    int startIdx = Mathf.Min(dragStartIndex, i);
                    int endIdx = Mathf.Max(dragStartIndex, i);
                    
                    // Clear previous selection if not using modifier keys
                    if (!Event.current.control && !Event.current.shift)
                    {
                        selectedPaths.Clear();
                    }
                    
                    // Add all items in drag range to selection
                    for (int idx = startIdx; idx <= endIdx; idx++)
                    {
                        selectedPaths.Add(texturePaths[idx]);
                    }
                    
                    // Update Unity's selection
                    Selection.objects = selectedPaths.Select(p => AssetDatabase.LoadAssetAtPath<Object>(p)).ToArray();
                    Repaint();
                    Event.current.Use();
                }
                
                if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.control)
                    {
                        // Control-click to toggle selection
                        if (selectedPaths.Contains(path))
                        {
                            selectedPaths.Remove(path);
                        }
                        else
                        {
                            selectedPaths.Add(path);
                            lastSelectedPath = path;
                        }
                    }
                    else if (Event.current.shift && lastSelectedPath != null)
                    {
                        // Shift-click to select a range
                        int lastIndex = texturePaths.IndexOf(lastSelectedPath);
                        int currentIndex = i;
                        int startIndex = Mathf.Min(lastIndex, currentIndex);
                        int endIndex = Mathf.Max(lastIndex, currentIndex);
                        
                        // Add all items in the range to selection
                        for (int idx = startIndex; idx <= endIndex; idx++)
                        {
                            selectedPaths.Add(texturePaths[idx]);
                        }
                    }
                    else
                    {
                        // Standard click or start of drag selection
                        isDragging = true;
                        dragStartIndex = i;
                        
                        // Clear selection unless Alt is held (Alt+click adds to selection)
                        if (!Event.current.alt)
                        {
                            selectedPaths.Clear();
                        }
                        
                        selectedPaths.Add(path);
                        lastSelectedPath = path;
                        lastClickTime = EditorApplication.timeSinceStartup;
                    }

                    // Update Unity's selection to match our internal selection
                    Selection.objects = selectedPaths.Select(p => AssetDatabase.LoadAssetAtPath<Object>(p)).ToArray();
                    Event.current.Use();
                }
            }
            GUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            
            // Remove the duplicate normal maps checkbox section, as it's already included in the "Texture Optimization Settings" section above
            // The duplicate section starts here
            /* EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Create tooltip content explaining why Box filter is better for normal maps
            GUIContent normalMapToggleContent = new GUIContent(
                "Set Normal Maps to Box Filter (Recommended)", 
                "Normal maps contain directional data that can be distorted by Kaiser filtering, causing visual artifacts. Box filter preserves the perpendicular relationship between XYZ components in normal maps, resulting in more accurate lighting and fewer artifacts."
            );

            // Use the GUIContent with the tooltip
            EditorGUI.BeginChangeCheck();
            setNormalMapsToBox = GUILayout.Toggle(setNormalMapsToBox, normalMapToggleContent);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(SET_NORMAL_MAPS_TO_BOX_KEY, setNormalMapsToBox);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal(); */
            
            EditorGUILayout.Space(15);
            
            // Calculate actionable textures count
            int texturesToUpdate = CountTexturesToUpdate(texturePaths);
            
            // Action button section
            float buttonWidth = 350;
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Enable button only if there are textures to update
            GUIStyle applyButtonStyle = texturesToUpdate > 0 ? actionButtonStyle : secondaryButtonStyle;
            
            // Create concise, informative button text based on current state
            string allButtonText = texturesToUpdate > 0 
                ? $"Apply Filters ({texturesToUpdate}/{texturePaths.Count} Need Updates)" 
                : (texturePaths.Count > 0 ? "All Textures Already Processed" : "No Textures Found");
            
            if (GUILayout.Button(allButtonText, applyButtonStyle, GUILayout.Height(45), GUILayout.Width(buttonWidth)))
            {
                string confirmMessage = setNormalMapsToBox ? 
                    $"Apply Kaiser filter to {texturesToUpdate} textures? (Normal maps will use Box filter)" : 
                    $"Apply Kaiser filter to {texturesToUpdate} textures including normal maps?";
                    
                if (EditorUtility.DisplayDialog(
                    "Apply Mipmap Filters", 
                    confirmMessage, 
                    "Apply", "Cancel"))
                {
                    ApplyMipmapFiltering(texturePaths);
                }
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // Show summary of what will happen
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel);
            infoStyle.alignment = TextAnchor.MiddleCenter;
            
            string filterText = setNormalMapsToBox ? 
                "Regular textures → Kaiser filter | Normal maps → Box filter" : 
                "All textures → Kaiser filter";
                
            string streamingText = enableMipStreaming ? " | Mip Streaming Enabled" : "";
            
            GUILayout.Label(filterText + streamingText, infoStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
        }
    }
    
    private void HandleKeyboardNavigation()
    {
        // Only process keyboard events if we have textures
        if (texturePaths.Count == 0)
            return;
            
        // Process keyboard navigation if this window has focus
        if (focusedWindow == this && Event.current.type == EventType.KeyDown)
        {
            // Initialize focusedIndex if not set
            if (focusedIndex < 0 && selectedPaths.Count > 0)
            {
                focusedIndex = texturePaths.IndexOf(selectedPaths.First());
            }
            else if (focusedIndex < 0)
            {
                focusedIndex = 0;
            }
            
            switch (Event.current.keyCode)
            {
                case KeyCode.UpArrow:
                    if (focusedIndex > 0)
                    {
                        focusedIndex--;
                        
                        // Handle shift key for range selection
                        if (Event.current.shift)
                        {
                            selectedPaths.Add(texturePaths[focusedIndex]);
                        }
                        else
                        {
                            selectedPaths.Clear();
                            selectedPaths.Add(texturePaths[focusedIndex]);
                        }
                        
                        // Scroll to the selected item if needed
                        EnsureItemVisible(focusedIndex);
                        lastSelectedPath = texturePaths[focusedIndex];
                        
                        // Update Unity's selection
                        Selection.objects = selectedPaths.Select(p => AssetDatabase.LoadAssetAtPath<Object>(p)).ToArray();
                        Repaint();
                        Event.current.Use();
                    }
                    break;
                    
                case KeyCode.DownArrow:
                    if (focusedIndex < texturePaths.Count - 1)
                    {
                        focusedIndex++;
                        
                        // Handle shift key for range selection
                        if (Event.current.shift)
                        {
                            selectedPaths.Add(texturePaths[focusedIndex]);
                        }
                        else
                        {
                            selectedPaths.Clear();
                            selectedPaths.Add(texturePaths[focusedIndex]);
                        }
                        
                        // Scroll to the selected item if needed
                        EnsureItemVisible(focusedIndex);
                        lastSelectedPath = texturePaths[focusedIndex];
                        
                        // Update Unity's selection
                        Selection.objects = selectedPaths.Select(p => AssetDatabase.LoadAssetAtPath<Object>(p)).ToArray();
                        Repaint();
                        Event.current.Use();
                    }
                    break;
            }
        }
    }
    
    private void EnsureItemVisible(int index)
    {
        // Calculate approximate height of an item row (40 pixels per row + some buffer)
        float itemHeight = 45;
        
        // Calculate the position of the item in the scroll view
        float itemY = index * itemHeight;
        
        // If item is above the current scroll position, scroll up to it
        if (itemY < scrollPosition.y)
        {
            scrollPosition.y = itemY;
        }
        // If item is below the visible area, scroll down to it
        else if (itemY > scrollPosition.y + position.height - 200) // 200 is approximate space for other UI elements
        {
            scrollPosition.y = itemY - position.height + 200;
        }
    }

    private void ScanTextures()
    {
        // Store the current Unity selection to restore it later
        Object[] previousSelection = Selection.objects;
        
        texturePaths.Clear();
        allTexturePaths.Clear(); // Clear the complete list too
        selectedPaths.Clear(); // Clear any existing selection
        lastSelectedPath = null; // Also reset the last selected path
        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { "Assets" });
        HashSet<string> sceneTexturePaths = new HashSet<string>();

        foreach (var obj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        foreach (var tex in mat.GetTexturePropertyNames())
                        {
                            Texture texture = mat.GetTexture(tex);
                            if (texture != null)
                            {
                                string path = AssetDatabase.GetAssetPath(texture);
                                if (!string.IsNullOrEmpty(path))
                                {
                                    sceneTexturePaths.Add(path); // Changed from lowercase 'add' to capitalized 'Add'
                                }
                            }
                        }
                    }
                }
            }
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (sceneTexturePaths.Contains(path) && IsIncludedInBuild(path) && SupportsKaiserMipmap(path))
            {
                // Add to master list but don't filter yet
                allTexturePaths.Add(path);
            }
        }
        
        // Apply filtering based on current checkbox state
        FilterTextures();
        
        // Restore the user's original selection in Unity instead of clearing it
        Selection.objects = previousSelection;
        
        scanCompleted = true;
        focusedIndex = -1; // Reset focused index without selecting anything
        Debug.Log($"Texture scan completed. {texturePaths.Count} textures found.");
    }
    
    // New method to filter textures based on checkbox state
    private void FilterTextures()
    {
        texturePaths.Clear();
        
        foreach (string path in allTexturePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.mipmapEnabled) // Only process textures with mipmaps enabled
            {
                bool isNormalMap = importer.textureType == TextureImporterType.NormalMap;
                bool isLightmap = importer.textureType == TextureImporterType.Lightmap;
                
                if (showAlreadyKaiserTextures)
                {
                    // Show all textures when checkbox is checked
                    texturePaths.Add(path);
                }
                else
                {
                    // Show texture if it needs processing
                    bool needsProcessing = false;
                    
                    if (isNormalMap)
                    {
                        // For normal maps: show if current filter doesn't match desired filter
                        if (setNormalMapsToBox)
                        {
                            needsProcessing = importer.mipmapFilter != TextureImporterMipFilter.BoxFilter;
                        }
                        else
                        {
                            needsProcessing = importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter;
                        }
                    }
                    else
                    {
                        // For regular textures: show if not using Kaiser filter
                        needsProcessing = importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter;
                    }
                    
                    // Also check mip streaming setting
                    if (!needsProcessing)
                    {
                        needsProcessing = importer.streamingMipmaps != enableMipStreaming;
                    }
                    
                    // Always show lightmaps or textures needing processing
                    if (isLightmap || needsProcessing)
                    {
                        texturePaths.Add(path);
                    }
                }
            }
        }
        
        EditorGUIUtility.PingObject(null);
        Repaint();
    }

    private bool IsIncludedInBuild(string path)
    {
        // Check if the asset is included in the build
        return !AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<Object>(path)).Contains("ExcludeFromBuild");
    }

    private bool SupportsKaiserMipmap(string path)
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        if (textureImporter != null && textureImporter.textureType == TextureImporterType.Lightmap)
        {
            // Always include lightmaps
            return true;
        }
        return textureImporter != null && textureImporter.mipmapEnabled;
    }

    private void ApplyMipmapFiltering(IEnumerable<string> pathsToProcess)
    {
        // Store the current Unity selection to restore it later
        Object[] previousSelection = Selection.objects;

        // Create a filtered list that excludes already processed textures
        List<string> actualPathsToProcess = new List<string>();
        foreach (string path in pathsToProcess)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (textureImporter != null)
            {
                // Skip textures that don't have mipmaps enabled
                if (!textureImporter.mipmapEnabled)
                    continue;

                bool isNormalMap = textureImporter.textureType == TextureImporterType.NormalMap;
                bool alreadyProcessed = false;
                
                if (isNormalMap)
                {
                    // Check if normal map already has the desired filter
                    alreadyProcessed = (setNormalMapsToBox && textureImporter.mipmapFilter == TextureImporterMipFilter.BoxFilter) ||
                                       (!setNormalMapsToBox && textureImporter.mipmapFilter == TextureImporterMipFilter.KaiserFilter);
                }
                else
                {
                    // Check if regular texture already has Kaiser filter
                    alreadyProcessed = textureImporter.mipmapFilter == TextureImporterMipFilter.KaiserFilter;
                }
                
                // Also check mip streaming setting - this was missing!
                alreadyProcessed = alreadyProcessed && (textureImporter.streamingMipmaps == enableMipStreaming);
                
                // Only add textures that need processing
                if (!alreadyProcessed)
                {
                    actualPathsToProcess.Add(path);
                }
            }
        }
        
        // If no textures need processing, show a message
        if (actualPathsToProcess.Count == 0)
        {
            Debug.Log("No textures need processing - all selected textures already have the correct filter applied.");
            EditorUtility.DisplayDialog("No Changes Needed", 
                "All selected textures already have the correct filter applied.", "OK");
            return;
        }
        
        int count = 0;
        foreach (string path in actualPathsToProcess)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (textureImporter != null)
            {
                if (textureImporter.textureType == TextureImporterType.NormalMap)
                {
                    textureImporter.mipmapFilter = setNormalMapsToBox ? TextureImporterMipFilter.BoxFilter : TextureImporterMipFilter.KaiserFilter;
                }
                else
                {
                    textureImporter.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                }
                
                // Apply mip streaming settings
                textureImporter.streamingMipmaps = enableMipStreaming;

                try
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to import asset at path {path}: {ex.Message}");
                }
            }
        }

        // Restore the user's selection
        Selection.objects = previousSelection;
        
        Debug.Log($"Kaiser filter applied to {count} textures successfully.");
        
        // Automatically rescan to refresh the list
        ScanTextures();
    }
    
    // To maintain backward compatibility with any existing code
    private void ApplyMipmapFiltering()
    {
        ApplyMipmapFiltering(selectedPaths);
    }
    
    // Draw a large preview tooltip for textures
    private void DrawLargePreviewTooltip(Texture texture)
    {
        if (texture == null) return;
        
        Rect mouseRect = new Rect(Event.current.mousePosition, Vector2.zero);
        mouseRect.width = 40;
        mouseRect.height = 40;
        
        // Check if mouse is over a texture preview
        if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
        {
            // Calculate tooltip size and position
            float previewSize = 200;
            Rect tooltipRect = new Rect(Event.current.mousePosition.x + 20, Event.current.mousePosition.y + 20, previewSize, previewSize);
            
            // Make sure tooltip stays inside window
            if (tooltipRect.xMax > position.width) tooltipRect.x = position.width - tooltipRect.width - 10;
            if (tooltipRect.yMax > position.height) tooltipRect.y = position.height - tooltipRect.height - 10;
            
            // Draw background for the tooltip
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(tooltipRect, EditorGUIUtility.whiteTexture);
            GUI.color = Color.white;
            
            // Draw texture preview
            Rect previewRect = new Rect(tooltipRect.x + 10, tooltipRect.y + 10, previewSize - 20, previewSize - 20);
            GUI.DrawTexture(previewRect, texture, ScaleMode.ScaleToFit);
            
            // Force repaint to keep updating the tooltip position as mouse moves
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
                Repaint();
        }
    }

    // Count textures that actually need updating
    private int CountTexturesToUpdate(IEnumerable<string> paths)
    {
        int count = 0;
        foreach (string path in paths)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (textureImporter != null && textureImporter.mipmapEnabled) // Only count textures with mipmaps enabled
            {
                bool isNormalMap = textureImporter.textureType == TextureImporterType.NormalMap;
                bool alreadyProcessed = false;
                
                if (isNormalMap)
                {
                    // Check if normal map already has the desired filter
                    alreadyProcessed = (setNormalMapsToBox && textureImporter.mipmapFilter == TextureImporterMipFilter.BoxFilter) ||
                                       (!setNormalMapsToBox && textureImporter.mipmapFilter == TextureImporterMipFilter.KaiserFilter);
                }
                else
                {
                    // Check if regular texture already has Kaiser filter
                    alreadyProcessed = textureImporter.mipmapFilter == TextureImporterMipFilter.KaiserFilter;
                }
                
                // Also check if mip streaming setting is already applied correctly
                alreadyProcessed = alreadyProcessed && (textureImporter.streamingMipmaps == enableMipStreaming);
                
                if (!alreadyProcessed)
                    count++;
            }
        }
        return count;
    }

    // Helper method to simplify filter names
    private string GetSimplifiedFilterName(TextureImporterMipFilter filter)
    {
        switch (filter)
        {
            case TextureImporterMipFilter.KaiserFilter:
                return "Kaiser";
            case TextureImporterMipFilter.BoxFilter:
                return "Box";
            default:
                string filterName = filter.ToString();
                if (filterName.EndsWith("Filter"))
                {
                    return filterName.Substring(0, filterName.Length - 6); // Remove "Filter" suffix
                }
                return filterName;
        }
    }

    // Helper methods for texture status
    private bool NeedsMipmapFilterUpdate(TextureImporter importer)
    {
        bool isNormalMap = importer.textureType == TextureImporterType.NormalMap;
        
        if (isNormalMap)
        {
            return setNormalMapsToBox ? 
                importer.mipmapFilter != TextureImporterMipFilter.BoxFilter : 
                importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter;
        }
        else
        {
            return importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter;
        }
    }

    private bool NeedsMipStreamingUpdate(TextureImporter importer)
    {
        return importer.streamingMipmaps != enableMipStreaming;
    }
    
    // Helper method to get texture format information
    private string GetTextureFormatInfo(Texture2D texture, TextureImporter importer)
    {
        // First check if it's a known texture type
        if (importer == null)
            return "Unknown";
            
        string textureType = "";
        if (importer.textureType == TextureImporterType.NormalMap)
            textureType = "Normal";
        else if (importer.textureType == TextureImporterType.Lightmap)
            textureType = "Lightmap";
        else if (importer.textureType == TextureImporterType.Cookie)
            textureType = "Cookie";
        else if (importer.textureType == TextureImporterType.GUI)
            textureType = "GUI";
        else
            textureType = "Default";
            
        // Get the actual format if possible
        string formatInfo = "";
        if (texture != null)
        {
            switch (texture.format)
            {
                case TextureFormat.DXT1:
                    formatInfo = "DXT1";
                    break;
                case TextureFormat.DXT5:
                    formatInfo = "DXT5";
                    break;
                case TextureFormat.RGB24:
                    formatInfo = "RGB24";
                    break;
                case TextureFormat.RGBA32:
                    formatInfo = "RGBA32";
                    break;
                case TextureFormat.BC7:
                    formatInfo = "BC7";
                    break;
                case TextureFormat.BC4:
                    formatInfo = "BC4";
                    break;
                case TextureFormat.BC5:
                    formatInfo = "BC5";
                    break;
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
                    formatInfo = "ASTC";
                    break;
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC2_RGB:
                    formatInfo = "ETC";
                    break;
                default:
                    // Empty for unknown formats, will just show the texture type
                    break;
            }
        }
        
        // Return both format and type
        if (!string.IsNullOrEmpty(formatInfo))
            return formatInfo + "\n" + textureType;
        else
            return textureType;
    }
}

// Fix the post processor class to properly handle auto application
public class KaiserPostProcessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        // Only process if auto-apply is enabled
        if (!SetKaiser.AutoApplyEnabled)
            return;
            
        TextureImporter textureImporter = (TextureImporter)assetImporter;
        
        // Never enable mipmaps if they're disabled - respect original settings
        if (!textureImporter.mipmapEnabled)
            return;
            
        // Only process new textures to avoid mass reimport
        if (System.IO.File.Exists(assetPath))
            return;
            
        // Determine the appropriate filter to apply based on texture type
        bool isNormalMap = textureImporter.textureType == TextureImporterType.NormalMap;
        
        if (isNormalMap && SetKaiser.UseBoxForNormalMaps)
        {
            textureImporter.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            Debug.Log($"Auto-applied Box filter to normal map: {assetPath}");
        }
        else
        {
            textureImporter.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            Debug.Log($"Auto-applied Kaiser filter to: {assetPath}");
        }
        
        // Apply mip streaming settings, but only for textures that have mipmaps enabled
        textureImporter.streamingMipmaps = SetKaiser.EnableMipStreaming;
        if (SetKaiser.EnableMipStreaming)
        {
            Debug.Log($"Enabled mip streaming for: {assetPath}");
        }
    }
    
    void OnPostprocessTexture(Texture2D texture)
    {
        // Only notify if this was a new texture, not existing ones
        if (SetKaiser.AutoApplyEnabled && !System.IO.File.Exists(assetPath))
        {
            SetKaiser.RequestRescan();
        }
    }
}