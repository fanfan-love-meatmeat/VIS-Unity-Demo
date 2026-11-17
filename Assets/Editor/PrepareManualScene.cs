#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VoxelIntrusion.Demo;

// Only scene/data wiring is new. Vehicle, VIS, Gizmos and depth display use source scripts.
[InitializeOnLoad]
public static class PrepareManualScene
{
    private const string ScenePath = "Assets/Scenes/Manual_VIS_Demo.unity";
    private static double nextDisplaySetupTime;
    private static int displaySetupAttempts;
    private static ObserverCropTransparency observerDisplay, sceneViewDisplay;
    static PrepareManualScene()
    {
        EditorApplication.delayCall += OpenOnStartup;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += (scene, mode) => QueueDisplaySetup();
        Camera.onPreCull += BeginSceneViewDisplay;
        Camera.onPostRender += EndSceneViewDisplay;
        QueueDisplaySetup();
    }

    private static void BeginSceneViewDisplay(Camera camera)
    {
        if (camera.cameraType!=CameraType.SceneView || observerDisplay==null || !observerDisplay.isActiveAndEnabled) return;
        sceneViewDisplay=observerDisplay;
        sceneViewDisplay.ApplyPreview();
    }

    private static void EndSceneViewDisplay(Camera camera)
    {
        if (camera.cameraType!=CameraType.SceneView || sceneViewDisplay==null) return;
        sceneViewDisplay.RestoreMaterials();
        sceneViewDisplay=null;
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) RefreshGamePreview();
        if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            QueueDisplaySetup();
    }

    private static void QueueDisplaySetup()
    {
        displaySetupAttempts=0;
        nextDisplaySetupTime=0;
        EditorApplication.update-=EnsureObserverDisplay;
        EditorApplication.update+=EnsureObserverDisplay;
    }

    // Domain reload can finish before Unity restores the scene. Wait for its objects.
    // Resolve existing components only: Inspector values and scene wiring belong to the user.
    private static void EnsureObserverDisplay()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<nextDisplaySetupTime) return;
        nextDisplaySetupTime=EditorApplication.timeSinceStartup+.25;
        GameObject observer=GameObject.Find("ObserverCamera");
        if (observer==null || observer.scene.path!=ScenePath)
        {
            if (++displaySetupAttempts>=40) EditorApplication.update-=EnsureObserverDisplay;
            return;
        }
        EditorApplication.update-=EnsureObserverDisplay;
        observerDisplay=observer.GetComponent<ObserverCropTransparency>();
        SceneView.RepaintAll();
    }

    private static void OpenOnStartup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!File.Exists("Assets/Editor/VIS_SOURCE_READY.txt")) return;
        if (SessionState.GetBool("VISSourceSceneOpened", false)) return;
        SessionState.SetBool("VISSourceSceneOpened", true);
        try
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning("VIS demo scene is missing. Restore the supplied scene or explicitly create a new one; startup will not rebuild or save scenes.");
                return;
            }
            if (SceneManager.GetActiveScene().path == "" && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(ScenePath);
            RefreshGamePreview();
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    private static void RefreshGamePreview()
    {
        if (SceneManager.GetActiveScene().path != ScenePath) return;
        Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (type == null) return;
        EditorWindow game = null;
        foreach (UnityEngine.Object candidate in Resources.FindObjectsOfTypeAll(type))
        {
            game = candidate as EditorWindow;
            if (game != null) break;
        }
        // Repaint the existing view without changing its layout or camera settings.
        // Populate the targets once for the edit-time Game preview as well.
        if (!EditorApplication.isPlaying)
        {
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.targetTexture != null && camera.gameObject.scene.path == ScenePath) camera.Render();
        }
        if (game != null) game.Repaint();
    }

    private static void BackupDirtyScene()
    {
        Scene previous = SceneManager.GetActiveScene();
        if (!previous.isDirty) return;
        Directory.CreateDirectory("Assets/Scenes");
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Before_VIS_setup.unity");
        EditorSceneManager.SaveScene(previous, path, true);
    }

    public static void Build()
    {
        // Initial defaults apply only to a new scene, never to an existing saved configuration.
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("The demo scene already exists. Its saved Inspector settings will not be overwritten.");
        if (GraphicsSettings.currentRenderPipeline != null)
            throw new InvalidOperationException("The original DepthDisplay / OnRenderImage path needs a Built-in 3D project.");
        DemoParameters p = JsonUtility.FromJson<DemoParameters>(File.ReadAllText("Assets/Editor/VISDemoParameters.json"));
        p.Validate();
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/CropModel/Generated");
        Directory.CreateDirectory("Assets/Material");
        Directory.CreateDirectory("Assets/RenderTextures");
        AssetDatabase.Refresh();
        var crop = DemoGeometry.CropCenters(p);
        var track = DemoGeometry.TrackCenters(p);
        // Original track centers are centered on their helper Transform, not at ground y=0.
        for (int i = 0; i < track.Count; i++) track[i] -= Vector3.up * p.trackHeight * .5f;
        const string centerPath = "Assets/CropModel/Generated/SyntheticCenters.bytes";
        using (var writer = new BinaryWriter(File.Create(centerPath)))
        {
            writer.Write(track.Count);
            foreach (var v in track) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
            writer.Write(crop.Count);
            foreach (var v in crop) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
        }
        AssetDatabase.ImportAsset(centerPath, ImportAssetOptions.ForceSynchronousImport);
        Mesh mesh = DemoGeometry.HalfCylinder(p.Radius, p.rowLength);
        const string meshPath = "Assets/CropModel/Generated/HalfCylinder.asset";
        AssetDatabase.CreateAsset(mesh, meshPath);
        Material canopy = Surface("CropRow", new Color(.23f,.62f,.29f), false);
        Material ground = Surface("Floor", new Color(.50f,.43f,.32f), false);
        Material trackMaterial = Surface("VehicleTracked", new Color(.4f,.4f,.4f,.1f), true);
        Material bodyMaterial = Surface("VehicleBody", new Color(.16f,.32f,.48f), false);
        Shader shader = Shader.Find("Custom/depth_shader_v8_sim2real_fix");
        if (shader == null) throw new InvalidOperationException("Original DepthShader.shader was not imported.");
        Material depthMaterial = new Material(shader) { name = "DepthMaterial" };
        depthMaterial.SetFloat("_VisualRangeMax",3); depthMaterial.SetFloat("_MinDistance",0);
        depthMaterial.SetFloat("_ClipThreshold",3); depthMaterial.SetFloat("_ColorMapMode",1);
        depthMaterial.SetColor("_NearColor",Color.white); depthMaterial.SetColor("_FarColor",Color.black);
        depthMaterial.EnableKeyword("_COLORMAPMODE_GRAYSCALE");
        foreach (string keyword in new[]{"_COLORMAPMODE_RAINBOW","_ENABLE_SPECKLE","_ENABLE_DYNAMIC_NOISE","_ENABLE_QUANTIZATION","_ENABLE_EDGE"})
            depthMaterial.DisableKeyword(keyword);
        foreach (string property in new[]{"_EnableSpeckle","_EnableDynamicNoise","_EnableQuantization","_EnableEdge"})
            depthMaterial.SetFloat(property,0);
        AssetDatabase.CreateAsset(depthMaterial,"Assets/Material/DepthMaterial.mat");
        RenderTexture rgb = TextureAsset("RGB", p), depth = TextureAsset("Depth", p);

        BackupDirtyScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Transform rows = new GameObject("CropRows").transform;
        for (int i = 0; i < p.rowCount; i++)
        {
            GameObject row = new GameObject("CropRow_"+(i+1));
            row.transform.SetParent(rows,false); row.transform.localPosition = new Vector3(p.RowCenterX(i),0,0);
            row.AddComponent<MeshFilter>().sharedMesh = mesh;
            row.AddComponent<MeshRenderer>().sharedMaterial = canopy;
        }
        Box("Floor",null,new Vector3(0,-.05f,p.rowLength/2),new Vector3(14,.1f,p.rowLength+5),ground);
        GameObject car = new GameObject("TrackedVehicle");
        car.SetActive(false); car.transform.position = new Vector3(p.RowCenterX(3),0,0);
        Transform left = Box("LeftTrack",car.transform,Vector3.zero,Vector3.one,trackMaterial).transform;
        Transform right = Box("RightTrack",car.transform,Vector3.zero,Vector3.one,trackMaterial).transform;
        Transform body = Box("VehicleBody",car.transform,Vector3.zero,Vector3.one,bodyMaterial).transform;
        TrackedVehicleSetting setting = car.AddComponent<TrackedVehicleSetting>();
        setting.leftTrack=left; setting.rightTrack=right; setting.vehicleBody=body;
        setting.trackSpacing=p.trackGauge; setting.trackWidth=p.trackWidth;
        setting.trackHeight=p.trackHeight; setting.trackLength=p.trackLength; setting.trackGroundClearance=0;
        setting.vehicleWidth=p.trackGauge-p.trackWidth; setting.vehicleHeight=.2f; setting.vehicleLength=1.6f;
        setting.UpdateDimensions();
        TrackedVehicleController movement = car.AddComponent<TrackedVehicleController>();
        movement.baseSpeed=p.forwardSpeed; movement.maxTrackSpeed=p.maxTrackSpeed;
        PIDController pid=car.AddComponent<PIDController>(); pid.Kp=.8f; pid.Ki=0; pid.Kd=.05f;
        ArrowVisualizer arrows=car.AddComponent<ArrowVisualizer>(); arrows.pidController=pid;
        car.AddComponent<KeyboardControlledVehicle>(); car.AddComponent<VehicleControlManager>();
        TrackVoxelizerController vis=car.AddComponent<TrackVoxelizerController>();
        vis.cropRowTransform=rows; vis.leftTrackTransform=left; vis.rightTrackTransform=right;
        vis.generatedCenterData=AssetDatabase.LoadAssetAtPath<TextAsset>(centerPath);
        vis.voxelSize=p.voxelSize; vis.updateFrequency=10; vis.showTrackVoxels=true; vis.onlyWhenSelected=false;
        GameObject rig=new GameObject("RGBD_Mount"); rig.transform.SetParent(car.transform,false);
        rig.transform.localPosition=new Vector3(0,p.cameraHeight,p.cameraForwardOffset);
        rig.transform.localRotation=Quaternion.Euler(p.cameraPitchDegrees,0,0);
        Camera rgbCamera=Sensor("RGB_Camera",rig.transform,rgb,p);
        Camera depthCamera=Sensor("Depth_Camera",rig.transform,depth,p);
        depthCamera.gameObject.AddComponent<DepthDisplay>().depthMaterial=depthMaterial;
        depthCamera.depthTextureMode=DepthTextureMode.Depth;
        ManualDemoHUD hud=car.AddComponent<ManualDemoHUD>();
        hud.startXPosition=car.transform.position.x; hud.startZPosition=car.transform.position.z;
        hud.voxelizer=vis; hud.vehicle=movement; hud.pid=pid;
        hud.rgbTexture=rgb; hud.depthTexture=depth; hud.rowCount=p.rowCount; hud.rowPitch=p.rowPitch;
        car.SetActive(true);
        GameObject view=new GameObject("ObserverCamera");
        view.transform.position=new Vector3(.3f,12.1f,12.1f);
        view.transform.rotation=Quaternion.Euler(90,0,0);
        Camera observer=view.AddComponent<Camera>(); observer.tag="MainCamera"; observer.farClipPlane=100;
        observer.orthographic=true; observer.orthographicSize=15;
        DroneFollowCamera follow=view.AddComponent<DroneFollowCamera>();
        follow.target=car.transform; follow.autoCalculateOffsetOnStart=false; follow.positionOffset=new Vector3(0,10,0);
        follow.enabled=false;
        view.AddComponent<ObserverCropTransparency>().cropRows=rows;
        GameObject sun=new GameObject("Directional Light");
        sun.transform.rotation=Quaternion.Euler(45,-30,0); sun.AddComponent<Light>().type=LightType.Directional;
        RenderSettings.ambientLight=new Color(.65f,.65f,.65f);

        // Exercise the actual copied detector with Unity Transforms in Edit Mode.
        vis.ForceUpdateOverlaps();
        bool initialClear=vis.TotalOverlapVolume==0;
        Vector3 start=car.transform.position;
        car.transform.position=new Vector3(start.x+.3f,0,3);
        vis.ForceUpdateOverlaps();
        int hitCount=vis.TotalUniqueDamagedVoxelCount;
        float hitVolume=vis.TotalOverlapVolume;
        vis.ForceUpdateOverlaps();
        bool deduplicated=vis.TotalUniqueDamagedVoxelCount==hitCount;
        car.transform.position=start; vis.ForceUpdateOverlaps(); vis.ResetUniqueDamageTracking();
        if (!initialClear || hitCount<=0 || hitVolume<=0 || !deduplicated || vis.TotalUniqueDamagedVoxelCount!=0)
            throw new InvalidOperationException("Original detector checks failed during scene preparation.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        Selection.activeGameObject=car;
        if (SceneView.lastActiveSceneView!=null)
        {
            SceneView.lastActiveSceneView.drawGizmos=true;
            SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,p.rowLength/2),Quaternion.Euler(50,-35,0),18);
        }
        File.WriteAllText("Library/VISSourceSetup.json",JsonUtility.ToJson(new SetupResult {
            scene=ScenePath,cropCenters=crop.Count,trackCenters=track.Count,
            cameras=3,initialClear=initialClear,nonzeroVIS=hitVolume,
            uniqueHitCount=hitCount,deduplicated=deduplicated,resetCleared=true},true));
        // Local preview cache, not a sensor/data export feature in the runtime scripts.
        try
        {
            Capture(observer,"Library/VISSourceObserver.png");
            Capture(rgbCamera,"Library/VISSourceRGB.png");
            Capture(depthCamera,"Library/VISSourceDepth.png");
        }
        catch (Exception captureError) { Debug.LogWarning("VIS preview capture: "+captureError.Message); }
        Debug.Log("VIS_SOURCE_SETUP_READY: open project -> prepared scene -> Play. Original source control, VIS and depth pipeline retained.");
    }

    [Serializable] private class SetupResult
    {
        public string scene;
        public int cropCenters,trackCenters,cameras,uniqueHitCount;
        public bool initialClear,deduplicated,resetCleared;
        public float nonzeroVIS;
    }
    private static Material Surface(string name,Color color,bool transparent)
    {
        Material material=new Material(Shader.Find("Standard")) {name=name,color=color};
        material.SetFloat("_Glossiness",.1f);
        if (transparent)
        {
            material.SetFloat("_Mode",3); material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha); material.SetInt("_ZWrite",0);
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); material.renderQueue=3000;
        }
        AssetDatabase.CreateAsset(material,"Assets/Material/"+name+".mat"); return material;
    }
    private static RenderTexture TextureAsset(string name,DemoParameters p)
    {
        var rt=new RenderTexture(p.cameraWidth,p.cameraHeightPixels,24,RenderTextureFormat.ARGB32) {name=name};
        AssetDatabase.CreateAsset(rt,"Assets/RenderTextures/"+name+".renderTexture"); return rt;
    }
    private static Camera Sensor(string name,Transform parent,RenderTexture rt,DemoParameters p)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false);
        var camera=go.AddComponent<Camera>(); camera.targetTexture=rt;
        camera.fieldOfView=p.cameraVerticalFov; camera.aspect=(float)p.cameraWidth/p.cameraHeightPixels;
        camera.nearClipPlane=p.cameraNearClip; camera.farClipPlane=p.cameraFarClip;
        camera.allowHDR=false; camera.allowMSAA=false; camera.depth=-1;
        return camera;
    }
    private static void Capture(Camera camera,string path)
    {
        RenderTexture original=camera.targetTexture;
        RenderTexture active=RenderTexture.active;
        RenderTexture target=original!=null ? original : RenderTexture.GetTemporary(1280,800,24);
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        Texture2D image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        camera.targetTexture=original; RenderTexture.active=active;
        if (original==null) RenderTexture.ReleaseTemporary(target);
    }
    private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
        go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size;
        go.GetComponent<MeshRenderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
}
#endif
