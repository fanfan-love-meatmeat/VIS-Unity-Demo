using UnityEngine;

// New glue only: display original outputs and reset the original controllers.
[ExecuteAlways]
public class ManualDemoHUD : MonoBehaviour
{
    public TrackVoxelizerController voxelizer;
    public TrackedVehicleController vehicle;
    public PIDController pid;
    public RenderTexture rgbTexture, depthTexture;
    public int rowCount = 8;
    public float rowPitch = 1.4f;
    [Header("Episode settings (world coordinates, m)")]
    [Tooltip("Vehicle-center start X used on entering Play and on reset with R. The nearest crop row defines the lateral-limit reference.")]
    public float startXPosition = -0.7f;
    [Tooltip("Vehicle-center start Z used on entering Play and on reset with R.")]
    public float startZPosition = 0f;
    [Tooltip("The episode succeeds when vehicle-center Z reaches this value. Must exceed Start Z Position.")]
    public float endZPosition = 23f;
    [Min(0.01f), Tooltip("Maximum lateral deviation of the vehicle center from the current crop-row center. Exceeding it ends the episode as a failure.")]
    public float maxLateralOffset = .7f;
    [Header("Panel positions (scaled to the Game view)")]
    [Tooltip("Instructions panel offsets from the left and top edges. Adjust in the Inspector only.")]
    public Vector2 helpPanelPosition = new Vector2(10, 10);
    [Tooltip("X: offset from the left edge. Y: offset from the normal six-row panel bottom to the view bottom. The top stays fixed when the end message extends downward. Adjust in the Inspector only.")]
    public Vector2 statsPanelBottomOffset = new Vector2(10, 12);
    [Tooltip("Steering-input panel offsets from the right and bottom edges. Adjust in the Inspector only.")]
    public Vector2 steeringPanelBottomOffset = new Vector2(10, 10);
    [InspectorName("RGB Panel Top Right Offset")]
    [Tooltip("RGB preview offsets from the right and top edges. Positive X moves left; positive Y moves down. Adjust in the Inspector only.")]
    public Vector2 rgbPanelTopRightOffset = new Vector2(10, 10);
    [Tooltip("Depth preview offsets from the right and top edges. Positive X moves left; positive Y moves down. Adjust in the Inspector only.")]
    public Vector2 depthPanelTopRightOffset = new Vector2(10, 280);
    private float rowCenterX;
    private bool episodeEnded;
    private string endReason;
    private int episodeNumber;
    private float previousEpisodeVIS;
    private bool hasPreviousEpisode;
    private Texture2D steeringGlow;
    private GUIStyle steeringTitleStyle, steeringArrowStyle, steeringKeyStyle;
    private GUIStyle previewTitleStyle;
    public bool PreviewWindowsDrawn { get; private set; }
    private static readonly string[] RunInstructions =
    {
        "1. Click Play to start.",
        "2. The vehicle moves forward at a constant 0.5 m/s.",
        "3. Use A/D or the left/right arrow keys to steer.",
        "4. Exceeding the lateral limit ends the episode.",
        "5. Reaching the endpoint also ends the episode.",
        "6. Press R to reset after the episode ends."
    };

    void Start()
    {
        if (!Application.isPlaying || vehicle == null || pid == null || voxelizer == null) return;
        Application.targetFrameRate = 30;
        ResetRun();
    }
    void Update()
    {
        if (!Application.isPlaying) return;
        if (!episodeEnded && Input.GetKeyDown(KeyCode.Space)) Time.timeScale = Time.timeScale == 0 ? 1 : 0;
        if (Input.GetKeyDown(KeyCode.R)) ResetRun();
    }
    void LateUpdate()
    {
        if (!Application.isPlaying || episodeEnded || vehicle == null || voxelizer == null) return;
        Vector3 position = vehicle.transform.position;
        if (position.z >= endZPosition) EndRun("Endpoint reached");
        else if (position.x < rowCenterX - maxLateralOffset || position.x > rowCenterX + maxLateralOffset)
            EndRun("Lateral limit exceeded");
    }
    private void EndRun(string reason)
    {
        episodeEnded = true;
        endReason = reason;
        vehicle.enabled = false;
        // Include the terminal pose in the existing instantaneous and episode VIS statistics.
        voxelizer.ForceUpdateOverlaps();
        Debug.Log("[VIS Demo] Episode ended: " + reason, this);
    }
    private void ResetRun()
    {
        Time.timeScale = 1;
        if (endZPosition <= startZPosition || maxLateralOffset <= 0 || rowPitch <= 0 || rowCount < 1)
        {
            episodeEnded = true;
            endReason = "Invalid episode settings";
            vehicle.enabled = false;
            Debug.LogError("[VIS Demo] End Z must exceed Start Z. The lateral limit, crop-row pitch and row count must be positive.", this);
            return;
        }
        // Snapshot before clearing the old run; the first Play initialization has no predecessor.
        if (episodeNumber > 0)
        {
            if (!episodeEnded) voxelizer.ForceUpdateOverlaps();
            previousEpisodeVIS = voxelizer.TotalUniqueDamagedVolume;
            hasPreviousEpisode = true;
        }
        episodeNumber++;
        episodeEnded = false;
        endReason = null;
        // Recompute the reference row on every reset, including after Inspector changes to Start X.
        int row = Mathf.Clamp(Mathf.RoundToInt(startXPosition / rowPitch + (rowCount - 1) * .5f), 0, rowCount - 1);
        rowCenterX = (row - (rowCount - 1) * .5f) * rowPitch;
        vehicle.enabled = true;
        vehicle.transform.SetPositionAndRotation(new Vector3(startXPosition, 0, startZPosition), Quaternion.identity);
        vehicle.ResetController(); pid.ResetPID();
        VehicleControlManager controls = vehicle.GetComponent<VehicleControlManager>();
        if (controls != null) controls.ResetControlCycle();
        voxelizer.ResetUniqueDamageTracking(); voxelizer.ForceUpdateOverlaps();
    }
    void OnGUI()
    {
        if (voxelizer == null || vehicle == null || pid == null) return;
        float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        DrawLeftPanels(width, height);
        DrawSteeringPanel(width, height);
        if (rgbTexture != null)
            DrawCameraPreview(ClampPanel(new Rect(width - 320 - rgbPanelTopRightOffset.x,
                rgbPanelTopRightOffset.y, 320, 260), width, height), "RGB / vehicle mounted", rgbTexture);
        if (depthTexture != null)
            DrawCameraPreview(ClampPanel(new Rect(width - 320 - depthPanelTopRightOffset.x,
                depthPanelTopRightOffset.y, 320, 260), width, height), "Depth / 0-3 m / near white", depthTexture);
        if (rgbTexture != null && depthTexture != null && Event.current.type == EventType.Repaint)
            PreviewWindowsDrawn = true;
        GUI.matrix = old;
    }

    private void DrawSteeringPanel(float width, float height)
    {
        const float panelWidth = 320, panelHeight = 144;
        Rect panel = ClampPanel(new Rect(width - panelWidth - steeringPanelBottomOffset.x,
            height - panelHeight - steeringPanelBottomOffset.y, panelWidth, panelHeight), width, height);
        if (steeringTitleStyle == null)
        {
            steeringTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            steeringArrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            steeringKeyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        }
        if (steeringGlow == null)
        {
            const int size = 64;
            steeringGlow = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SteeringKeyGlow", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float radiusSquared = dx * dx + dy * dy;
                    float alpha = Mathf.Exp(-3f * radiusSquared) * Mathf.Clamp01(1f - radiusSquared) * .85f;
                    pixels[y * size + x] = new Color(.1f, .8f, 1f, alpha);
                }
            steeringGlow.SetPixels(pixels);
            steeringGlow.Apply(false, true);
        }
        // Read physical keys directly so both held keys can be shown independently.
        bool left = Application.isPlaying && (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow));
        bool right = Application.isPlaying && (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));
        GUI.Box(panel, GUIContent.none);
        GUI.BeginGroup(panel);
        GUI.Label(new Rect(14, 10, panelWidth - 28, 28), "Steering input", steeringTitleStyle);
        DrawSteeringKey(new Rect(42, 48, 104, 78), "←", "A / Left", left);
        DrawSteeringKey(new Rect(174, 48, 104, 78), "→", "D / Right", right);
        GUI.EndGroup();
    }

    private void DrawSteeringKey(Rect rect, string arrow, string keys, bool pressed)
    {
        Color oldColor = GUI.color;
        if (pressed)
            GUI.DrawTexture(new Rect(rect.x - 18, rect.y - 16, rect.width + 36, rect.height + 32), steeringGlow, ScaleMode.StretchToFill, true);
        GUI.color = pressed ? new Color(.1f, .8f, 1f) : new Color(.38f, .42f, .46f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = pressed ? new Color(.08f, .25f, .32f) : new Color(.12f, .14f, .16f);
        GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), Texture2D.whiteTexture);
        GUI.color = pressed ? new Color(.55f, .95f, 1f) : Color.white;
        GUI.Label(new Rect(rect.x, rect.y - 2, rect.width, 56), arrow, steeringArrowStyle);
        GUI.Label(new Rect(rect.x, rect.y + 51, rect.width, 24), keys, steeringKeyStyle);
        GUI.color = oldColor;
    }
    private void DrawLeftPanels(float width, float height)
    {
        const float panelWidth = 360, padding = 14;
        GUIStyle text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = false };
        GUIStyle title = new GUIStyle(text) { fontSize = 16, fontStyle = FontStyle.Bold };
        GUIStyle value = new GUIStyle(text) { alignment = TextAnchor.MiddleRight };
        text.alignment = TextAnchor.MiddleLeft;
        float rowHeight = Mathf.Max(28, text.CalcSize(new GUIContent("Previous episode proxy")).y + 6);
        float titleHeight = Mathf.Max(28, title.CalcSize(new GUIContent("Instructions")).y + 6);

        GUIStyle helpText = new GUIStyle(text) { wordWrap = true };
        float helpContentWidth = panelWidth - padding * 2;
        float helpHeight = padding * 2 + titleHeight;
        foreach (string instruction in RunInstructions)
            helpHeight += Mathf.Max(rowHeight, helpText.CalcHeight(new GUIContent(instruction), helpContentWidth) + 6);
        Rect help = ClampPanel(new Rect(helpPanelPosition.x, helpPanelPosition.y, panelWidth, helpHeight), width, height);
        GUI.Box(help, GUIContent.none);
        GUI.BeginGroup(help);
        GUI.Label(new Rect(padding, padding, panelWidth - padding * 2, titleHeight), "Instructions", title);
        float helpY = padding + titleHeight;
        foreach (string instruction in RunInstructions)
        {
            float instructionHeight = Mathf.Max(rowHeight, helpText.CalcHeight(new GUIContent(instruction), helpContentWidth) + 6);
            GUI.Label(new Rect(padding, helpY, helpContentWidth, instructionHeight), instruction, helpText);
            helpY += instructionHeight;
        }
        GUI.EndGroup();

        // Explicit row heights avoid GUILayout's font-dependent clipping in a fixed-height area.
        const int statsRows = 8;
        float statsBaseHeight = padding * 2 + titleHeight + rowHeight * statsRows;
        // Preserve the existing top anchor; the two speed rows extend the panel downward.
        float statsAnchorHeight = padding * 2 + titleHeight + rowHeight * 6;
        // Use the normal panel's position in both states; reserve the final row before clamping.
        Rect stats = ClampPanel(new Rect(statsPanelBottomOffset.x, height - statsAnchorHeight - statsPanelBottomOffset.y, panelWidth, statsBaseHeight + rowHeight), width, height);
        stats.height = statsBaseHeight + (episodeEnded ? rowHeight : 0);
        GUI.Box(stats, GUIContent.none);
        GUI.BeginGroup(stats);
        GUI.Label(new Rect(padding, padding, panelWidth - padding * 2, titleHeight), "VIS statistics", title);
        float rowY = padding + titleHeight;
        DrawStatRow(rowY, rowHeight, "Episode", episodeNumber.ToString(), text, value);
        DrawStatRow(rowY + rowHeight, rowHeight, "Left-track VIS", voxelizer.LeftOverlapVolume.ToString("F6") + " m³", text, value);
        DrawStatRow(rowY + rowHeight * 2, rowHeight, "Right-track VIS", voxelizer.RightOverlapVolume.ToString("F6") + " m³", text, value);
        DrawStatRow(rowY + rowHeight * 3, rowHeight, "Per-update VIS", voxelizer.TotalOverlapVolume.ToString("F6") + " m³", text, value);
        DrawStatRow(rowY + rowHeight * 4, rowHeight, "Episode VIS proxy", voxelizer.TotalUniqueDamagedVolume.ToString("F6") + " m³", text, value);
        DrawStatRow(rowY + rowHeight * 5, rowHeight, "Previous episode proxy", hasPreviousEpisode ? previousEpisodeVIS.ToString("F6") + " m³" : "—", text, value);
        DrawStatRow(rowY + rowHeight * 6, rowHeight, "Left-track target speed", vehicle.LeftTrackTargetSpeed.ToString("F3") + " m/s", text, value);
        DrawStatRow(rowY + rowHeight * 7, rowHeight, "Right-track target speed", vehicle.RightTrackTargetSpeed.ToString("F3") + " m/s", text, value);
        if (episodeEnded)
        {
            GUIStyle endStatus = new GUIStyle(text);
            endStatus.normal.textColor = Color.yellow;
            GUI.Label(new Rect(padding, rowY + rowHeight * statsRows, panelWidth - padding * 2, rowHeight), endReason + "; press R to reset", endStatus);
        }
        GUI.EndGroup();
    }
    private static Rect ClampPanel(Rect rect, float width, float height)
    {
        rect.x = Mathf.Clamp(rect.x, 0, Mathf.Max(0, width - rect.width));
        rect.y = Mathf.Clamp(rect.y, 0, Mathf.Max(0, height - rect.height));
        return rect;
    }
    private void DrawStatRow(float y, float height, string label, string content, GUIStyle labelStyle, GUIStyle valueStyle)
    {
        GUI.Label(new Rect(14, y, 166, height), label, labelStyle);
        GUI.Label(new Rect(188, y, 158, height), content, valueStyle);
    }
    private void DrawCameraPreview(Rect rect, string title, RenderTexture texture)
    {
        if (previewTitleStyle == null)
            previewTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 8, rect.y, rect.width - 16, 24), title, previewTitleStyle);
        GUI.DrawTexture(new Rect(rect.x + 8, rect.y + 24, rect.width - 16, rect.height - 32), texture, ScaleMode.ScaleToFit, false);
    }
    void OnDisable()
    {
        Time.timeScale = 1;
        if (steeringGlow != null)
        {
            if (Application.isPlaying) Destroy(steeringGlow);
            else DestroyImmediate(steeringGlow);
            steeringGlow = null;
        }
    }
}
