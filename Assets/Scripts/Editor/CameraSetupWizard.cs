#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Framework.Cameras;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Framework.Cameras.EditorTools
{
    /// <summary>
    /// 一键安装场景相机套件：Brain / PositionComposer / Confiner2D / ImpulseListener /
    /// CameraBoundary / Global Volume (Bloom+Vignette) / ImpulseSource，并把 CameraManager
    /// 的 Main Vcam 连接到场景里的 CinemachineCamera-player。
    /// 菜单：Tools/Camera/Setup Scene。重复点击是幂等的。
    /// </summary>
    public static class CameraSetupWizard
    {
        private const string PlayerVcamName = "CinemachineCamera-player";
        private const string CameraManagerName = "CameraManager";
        private const string BoundaryName = "CameraBoundary";
        private const string VolumeName = "Global Volume";
        private const string ProfileFolder = "Assets/Settings";
        private const string ProfilePath = "Assets/Settings/CameraVolumeProfile.asset";

        [MenuItem("Tools/Camera/Setup Scene")]
        public static void SetupScene()
        {
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Camera Setup Scene");

            // 1. Main Camera + Brain
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("[CameraSetup] 场景里找不到 Main Camera。先自己建一个主相机再运行。");
                return;
            }
            EnsureBrain(mainCam);

            // 2. Player vcam
            var playerVcam = FindInScene<CinemachineCamera>(PlayerVcamName);
            if (playerVcam == null)
            {
                Debug.LogError($"[CameraSetup] 场景里找不到名为 \"{PlayerVcamName}\" 的 CinemachineCamera。请先在 Hierarchy 里创建并命名。");
                return;
            }
            ConfigurePlayerVcam(playerVcam);

            // 3. Boundary + Confiner
            var boundary = EnsureCameraBoundary();
            EnsureConfiner(playerVcam, boundary);

            // 4. Impulse Listener on vcam
            EnsureComponent<CinemachineImpulseListener>(playerVcam.gameObject, listener =>
            {
                listener.Use2DDistance = true;
                listener.Gain = 1f;
            });

            // 5. CameraManager wiring
            var manager = Object.FindFirstObjectByType<CameraManager>();
            if (manager == null)
            {
                Debug.LogWarning($"[CameraSetup] 场景里没有 CameraManager。已跳过其配置（请先在 Hierarchy 里建个空物体挂上 CameraManager 脚本）。");
            }
            else
            {
                ConfigureCameraManager(manager, playerVcam);
            }

            // 6. Global Volume
            EnsureVolume();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CameraSetup] 完成。可再次运行，幂等。");
        }

        // ---------- 步骤实现 ----------

        private static void EnsureBrain(Camera cam)
        {
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = Undo.AddComponent<CinemachineBrain>(cam.gameObject);
            }
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, 1.5f);
            EditorUtility.SetDirty(brain);
        }

        private static void ConfigurePlayerVcam(CinemachineCamera vcam)
        {
            vcam.Priority = 10;

            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            if (lens.OrthographicSize < 0.01f) lens.OrthographicSize = 6f;
            vcam.Lens = lens;

            // PositionComposer: 死区 / 阻尼
            var composer = vcam.GetComponent<CinemachinePositionComposer>();
            if (composer == null)
                composer = Undo.AddComponent<CinemachinePositionComposer>(vcam.gameObject);

            composer.Damping = new Vector3(1f, 1f, 0f);
            composer.DeadZone.Enabled = true;
            composer.DeadZone.Size = new Vector2(0.2f, 0.15f);
            composer.HardLimits.Enabled = false;
            composer.Composition.ScreenPosition = new Vector2(0f, 0f);
            composer.CameraDistance = 10f;
            EditorUtility.SetDirty(composer);
            EditorUtility.SetDirty(vcam);
        }

        private static GameObject EnsureCameraBoundary()
        {
            var go = GameObject.Find(BoundaryName);
            if (go == null)
            {
                go = new GameObject(BoundaryName);
                Undo.RegisterCreatedObjectUndo(go, "Create CameraBoundary");
                go.transform.position = Vector3.zero;
            }

            var poly = go.GetComponent<PolygonCollider2D>();
            if (poly == null)
                poly = Undo.AddComponent<PolygonCollider2D>(go);

            poly.isTrigger = true;

            // 默认给一个 30x18 的矩形（后续你自己编辑形状）
            if (poly.GetTotalPointCount() == 0 || poly.points.Length == 0)
            {
                poly.points = new[]
                {
                    new Vector2(-15f, -9f),
                    new Vector2( 15f, -9f),
                    new Vector2( 15f,  9f),
                    new Vector2(-15f,  9f),
                };
            }
            EditorUtility.SetDirty(poly);
            return go;
        }

        private static void EnsureConfiner(CinemachineCamera vcam, GameObject boundary)
        {
            var confiner = vcam.GetComponent<CinemachineConfiner2D>();
            if (confiner == null)
                confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);

            confiner.BoundingShape2D = boundary.GetComponent<Collider2D>();
            confiner.Damping = 0.5f;
            confiner.InvalidateBoundingShapeCache();
            EditorUtility.SetDirty(confiner);
        }

        private static void ConfigureCameraManager(CameraManager manager, CinemachineCamera playerVcam)
        {
            var so = new SerializedObject(manager);
            var mainVcamProp = so.FindProperty("_mainVcam");
            if (mainVcamProp != null && mainVcamProp.objectReferenceValue == null)
                mainVcamProp.objectReferenceValue = playerVcam;

            // 确保 Cameras 列表为空（如果用户误把 player vcam 放进去了，清掉）
            var camsProp = so.FindProperty("_cameras");
            if (camsProp != null)
            {
                for (int i = camsProp.arraySize - 1; i >= 0; i--)
                {
                    var elem = camsProp.GetArrayElementAtIndex(i);
                    var keyProp = elem.FindPropertyRelative("key");
                    var camProp = elem.FindPropertyRelative("cam");
                    bool emptyKey = keyProp != null && string.IsNullOrEmpty(keyProp.stringValue);
                    bool isPlayerVcam = camProp != null && camProp.objectReferenceValue == playerVcam;
                    if (emptyKey || isPlayerVcam)
                        camsProp.DeleteArrayElementAtIndex(i);
                }
            }

            so.ApplyModifiedProperties();

            // ImpulseSource 放在 CameraManager 上
            var src = manager.GetComponent<CinemachineImpulseSource>();
            if (src == null)
                src = Undo.AddComponent<CinemachineImpulseSource>(manager.gameObject);

            // 把 Default Impulse Source 字段连上
            var so2 = new SerializedObject(manager);
            var srcProp = so2.FindProperty("_defaultImpulseSource");
            if (srcProp != null && srcProp.objectReferenceValue == null)
            {
                srcProp.objectReferenceValue = src;
                so2.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(manager);
        }

        private static void EnsureVolume()
        {
            // Profile asset
            if (!Directory.Exists(ProfileFolder))
                Directory.CreateDirectory(ProfileFolder);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            if (!profile.TryGet<Bloom>(out var bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.9f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.4f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.7f;

            if (!profile.TryGet<Vignette>(out var vig))
                vig = profile.Add<Vignette>(true);
            vig.active = true;
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.3f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.4f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Global Volume GO
            var volGo = GameObject.Find(VolumeName);
            if (volGo == null)
            {
                volGo = new GameObject(VolumeName);
                Undo.RegisterCreatedObjectUndo(volGo, "Create Global Volume");
            }

            var vol = volGo.GetComponent<Volume>();
            if (vol == null)
                vol = Undo.AddComponent<Volume>(volGo);

            vol.isGlobal = true;
            vol.priority = 0;
            vol.weight = 1f;
            vol.sharedProfile = profile;
            EditorUtility.SetDirty(vol);

            // 确保 Main Camera 的 Post Processing 打开
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var urpData = mainCam.GetUniversalAdditionalCameraData();
                if (urpData != null)
                {
                    urpData.renderPostProcessing = true;
                    EditorUtility.SetDirty(urpData);
                }
            }
        }

        // ---------- 工具 ----------

        private static T EnsureComponent<T>(GameObject go, System.Action<T> configure = null) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            configure?.Invoke(c);
            EditorUtility.SetDirty(c);
            return c;
        }

        private static T FindInScene<T>(string goName) where T : Component
        {
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(c => c.gameObject.name == goName);
        }
    }
}
#endif
