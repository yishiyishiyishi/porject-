#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Framework;

namespace Framework.Art.EditorTools
{
    /// <summary>
    /// 三层视差背景生成器。工作流程：
    ///   1. 把 3 张图拖进 Project（建议 Assets/Art/Environment/BG/）。
    ///   2. 菜单 Tools/Art/Create Parallax Backdrop 打开窗口。
    ///   3. 三个槽分别拖 Near / Mid / Far 贴图，点 Create。
    ///
    /// 生成物：
    ///   ParallaxBackdrop (空父物体)
    ///     Near_FG   SpriteRenderer  Z=-2   DepthLock
    ///     Mid_BG    SpriteRenderer  Z=10   DepthLock
    ///     Far_BG    SpriteRenderer  Z=50   DepthLock
    ///
    /// 自动：
    ///   - 如果贴图不是 Sprite(2D)，自动修改 TextureImporter 并 reimport
    ///   - 按透视相机 FOV + 距离计算出可见高度，等比缩放每层 Sprite 填满画面高度
    /// </summary>
    public class ParallaxBackdropWizard : EditorWindow
    {
        private Texture2D _near, _mid, _far;
        // 默认对齐 ZDepth 三档背景（BackgroundNear=10 / Mid=30 / Far=80）。
        // 若 Near 槽实际要塞"前景遮挡片"，手动改成 ZDepth.ForegroundNear(-2)。
        private float _nearZ = ZDepth.BackgroundNear;
        private float _midZ  = ZDepth.BackgroundMid;
        private float _farZ  = ZDepth.BackgroundFar;
        private float _extraWidthMul = 1.5f; // 宽度冗余，便于横向滚动
        private string _sortingLayer = "Default";
        // 近 → 远：sortingOrder 递减，保证同 layer 渲染顺序对
        private int _nearOrder = 0, _midOrder = -10, _farOrder = -20;

        [MenuItem("Tools/Art/Create Parallax Backdrop")]
        public static void Open() => GetWindow<ParallaxBackdropWizard>("Parallax Backdrop").Show();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Layer Textures", EditorStyles.boldLabel);
            _near = (Texture2D)EditorGUILayout.ObjectField("Near (bg1, 近景)", _near, typeof(Texture2D), false);
            _mid  = (Texture2D)EditorGUILayout.ObjectField("Mid  (bg2, 中景)", _mid,  typeof(Texture2D), false);
            _far  = (Texture2D)EditorGUILayout.ObjectField("Far  (bg3, 远景)", _far,  typeof(Texture2D), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Z Positions (与 Framework.ZDepth 常量对齐)", EditorStyles.boldLabel);
            _nearZ = EditorGUILayout.FloatField("Near Z", _nearZ);
            _midZ  = EditorGUILayout.FloatField("Mid Z",  _midZ);
            _farZ  = EditorGUILayout.FloatField("Far Z",  _farZ);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _extraWidthMul = EditorGUILayout.Slider("宽度冗余 ×", _extraWidthMul, 1f, 4f);
            _sortingLayer = EditorGUILayout.TextField("Sorting Layer", _sortingLayer);
            _nearOrder = EditorGUILayout.IntField("Near Order",  _nearOrder);
            _midOrder  = EditorGUILayout.IntField("Mid  Order",  _midOrder);
            _farOrder  = EditorGUILayout.IntField("Far  Order",  _farOrder);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_near == null && _mid == null && _far == null))
            {
                if (GUILayout.Button("Create / Update in Scene", GUILayout.Height(32)))
                    Create();
            }

            EditorGUILayout.HelpBox(
                "相机应已是 Perspective（跑过 Tools/Camera/Apply 3D Perspective），脚本会按主相机 FOV 估算缩放。\n" +
                "默认三槽都是背景（Z=10/30/80）。如果 Near 实际是前景遮挡片，手动把 Near Z 改成 -2，并保证 PNG 带 Alpha。",
                MessageType.Info);
        }

        // ---------- 核心 ----------

        private void Create()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("缺 Main Camera", "场景里找不到 Main Camera。", "OK");
                return;
            }
            if (cam.orthographic)
                EditorUtility.DisplayDialog("提示", "主相机当前是 Orthographic；视差在正交相机下不会自动产生。先跑 Tools/Camera/Apply 3D Perspective 更有效。", "继续");

            var root = GameObject.Find("ParallaxBackdrop");
            if (root == null)
            {
                root = new GameObject("ParallaxBackdrop");
                Undo.RegisterCreatedObjectUndo(root, "Create ParallaxBackdrop");
            }

            // 相机信息：位置 Z、FOV、aspect
            float camZ = cam.transform.position.z;
            float fovRad = cam.fieldOfView * Mathf.Deg2Rad;

            CreateLayer(root.transform, "Far_BG",  _far,  _farZ,  camZ, fovRad, cam.aspect, _farOrder);
            CreateLayer(root.transform, "Mid_BG",  _mid,  _midZ,  camZ, fovRad, cam.aspect, _midOrder);
            CreateLayer(root.transform, "Near_FG", _near, _nearZ, camZ, fovRad, cam.aspect, _nearOrder);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[ParallaxWizard] 完成。可重复执行覆盖。");
        }

        private void CreateLayer(Transform parent, string name, Texture2D tex, float z, float camZ,
                                 float fovRad, float aspect, int sortingOrder)
        {
            if (tex == null) return;

            // 确保是 Sprite 导入
            var sprite = EnsureSprite(tex);
            if (sprite == null)
            {
                Debug.LogError($"[ParallaxWizard] 无法把 {tex.name} 转成 Sprite。");
                return;
            }

            // 找或建子物体
            var go = parent.Find(name)?.gameObject;
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create Layer");
                go.transform.SetParent(parent, false);
            }

            // 位置
            var pos = go.transform.position;
            pos.z = z;
            go.transform.position = pos;

            // 缩放：让 Sprite 在该 Z 下满屏高度 × extraWidthMul
            float distance = Mathf.Abs(z - camZ);
            float visibleHeight = 2f * distance * Mathf.Tan(fovRad * 0.5f);
            float visibleWidth = visibleHeight * aspect;

            // Sprite 原始像素尺寸 → 世界单位（Sprite.pixelsPerUnit = 100 默认）
            float pxPerUnit = sprite.pixelsPerUnit;
            float spriteWorldH = sprite.rect.height / pxPerUnit;
            float spriteWorldW = sprite.rect.width  / pxPerUnit;

            float scaleY = visibleHeight / spriteWorldH;
            float scaleX = (visibleWidth * _extraWidthMul) / spriteWorldW;
            // 保证各向同性：取 Y 为基准再看 X 是否够，不够再拉 X
            float s = Mathf.Max(scaleX, scaleY);
            go.transform.localScale = new Vector3(s, s, 1f);

            // SpriteRenderer
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(go);
            sr.sprite = sprite;
            sr.sortingLayerName = _sortingLayer;
            sr.sortingOrder = sortingOrder;

            // DepthLock 锁 Z
            var dl = go.GetComponent<DepthLock>();
            if (dl == null) dl = Undo.AddComponent<DepthLock>(go);
            dl.targetZ = z;

            EditorUtility.SetDirty(go);
        }

        private Sprite EnsureSprite(Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return null;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (importer.alphaIsTransparency != true)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            // 背景图默认不需要 mesh 紧缩，保留 FullRect 以便大尺度缩放
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                dirty = true;
            }
            if (dirty)
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
