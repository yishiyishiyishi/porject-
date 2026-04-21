# GAME1

Unity 6 + URP + 新 Input System + Cinemachine 3.x。2.5D（透视相机 + 2D Sprite）。

本文档按**职能**拆分，对齐主程与美术的颗粒度：只写"我做了什么、还要做什么"和"美术要产出什么、按什么规范产出"。

---

## 一、程序部分（本人负责）

### 1. 已完成

#### 工程与协作
- 工程初始化，远端 `yishiyishiyishi/porject-` 已接通，Unity 版 `.gitignore` 清干净了 `Library/`、`Temp/`、`Logs/`、`*.csproj/.sln` 等生成物。
- 只保留 `Assets/`、`Packages/`、`ProjectSettings/` 入库。

#### 输入层
- 使用新 Input System，动作表 `Assets/InputSystem_Actions.inputactions`。
- `Framework/Input/IPlayerInput` + `PlayerInputReader`：对外只暴露 `Move` / `Jump` / `Dash` / `Interact` 语义，不与设备耦合。

#### 角色 / Actor 框架
- `Framework/Actor`：`Actor` 基类、`ActorModule`（能力模块基类）、`ActorState`（状态数据）、`ActionGate`（能力开关闸）。
- 玩家：`Player/PlayerActor` + 三个能力模块 `MoveModule` / `JumpModule` / `DashModule`。移动手感适配自外部参考项目（加速 / 刹车 / 空中折损分离）。
- 感知：`Framework/Sensors/GroundSensor` 接地判定。

#### 叙事 / 对话 / 交互
- `Framework/Dialogue`：`DialogueLine`、`DialogueTrigger`、`IDialogueRunner`、`SimpleDialogueRunner`、`DialogueAdvancer`。
- `Framework/NarrativeRuntime` + `Core/Narrative/NarrativeState`：叙事运行时与全局状态。
- `Player/DialoguePlayerFreeze`：对话期间冻结玩家控制。
- `Framework/Interaction`：`Interactable` + `InteractorModule`（可交互物 + 玩家身上的探针）。

#### 相机（2.5D）
- `Framework/Camera/CameraManager`：单例，统一接口。
  - `SwitchTo(key)`：多 vcam 优先级切换（State-Driven 式）。
  - `LockCameraPosition(bool)`：演出锁镜头。
  - `SetOrthoSize` / `SetFollowOffset`：协程平滑修改镜头参数。
  - `Shake` / `ShakeQuick`：通过 `CinemachineImpulseSource` 触发震屏。
- `Framework/Camera/CameraTrigger`：Trigger 进入切相机、离开回主相机。
- `Framework/DepthLock` + `Framework/ZDepth`：Z 轴深度锁定与协议常量。
- `Assets/Scripts/Editor/CameraSetupWizard`（Editor）：菜单 `Tools/Camera/Setup Scene`、`Apply 3D Perspective`、`Revert To Orthographic`，一键把场景相机全家桶（Brain / PositionComposer / Confiner2D / ImpulseListener/Source / Global Volume / Bloom+Vignette+DoF / MSAA 4x / 透视 FOV / Transparency Sort Perspective）配好。

#### UI / HUD
- `Framework/UI/UIManager` + `UIView`：View 注册 / 显隐栈式管理。
- 现成 View：`DialogueView`（对白 UI）、`FadeView`（黑场转场）、`InteractPromptView`（交互提示）。
- `Framework/UI/UIEvents`：UI 相关事件定义。

#### 存档 / 读档
- `Framework/Save/SaveData` + `SaveManager` + `SaveEvents`：存档数据结构、读写、事件。
- `SaveDebugKeys`：调试热键。
- `Player/PlayerSavePoint`：玩家侧存档点。

#### 关卡 / 场景
- `Framework/Scene/LevelLoader` + `LevelLoadEvents`：异步加载、过渡事件。
- `LevelPortal` + `SceneSpawnPoint`：关卡间传送点与落地点。

#### 音频
- `Framework/Audio/AudioManager` + `AudioCue` + `MusicTrack` + `AudioFilterPreset`：BGM / SE / 滤镜预设。

#### 敌人 / NPC AI（Framework/AI）
- `StateMachine<T>`：极简泛型 FSM，链式 `Configure(state).OnEnter/OnTick/OnFixedTick/OnExit`，带 `TimeInState`、`ReenterState()`（连招强制重入）、`OnTransition` 事件。
- `Health` + `IDamageable` + `DamageInfo`：通用血量模块，敌我共用。命中后统一派发 `ActorDamaged`/`ActorDied` 到 EventBus，击退通过 `Rigidbody2D.linearVelocity` 直接赋值。
- `EnvironmentProbe`：前方墙 + 前方脚下悬崖的 Raycast，`RaycastNonAlloc` + 自身 Rigidbody 过滤，避免自击。
- `EnemySenses`：按 Tag 查玩家、距离 / 视野夹角 / 可选视线遮挡，带迟滞环（`detectionRadius` 触发、`loseRadius` 脱离）防抖。
- `EnemyLocomotion`：统一走路接口（`Request(dir, speed)` / `Stop()`），指令持久化不抖脚，墙/悬崖联动自动停步。
- `EnemyBrain`：`Patrol / Chase / Attack / Stunned / Dead` 五态 FSM，攻击切 `windup / active / recovery` 三段，`OverlapCircle` 命中判定，连招走 `ReenterState()`。
- `EnemyActor`：Actor 子类 + faction 阵营字段。
- `NpcWanderer`：NPC 轻量三态（Idle/Wander/Talk），`SetTalking()` 对接对话。

#### 基础设施
- `Core/EventBus`：泛型事件总线，贯穿所有系统间通信（Publish 逐订阅者 try/catch，单订阅者异常不掐整条链）。
- `MathTools`：通用数值 / 向量扩展。

### 2. 待做（程序）

- **相机触发网**：关键转场 / Boss 房 / 剧情机位的 `CameraTrigger` 布点，按 `ZDepth` 规范在场景中实际摆位。
- **帧级战斗反馈**：命中定格、`LocalTimeScale` 短暂拉低、震屏预设分级（轻/中/重）与统一入口。
- **玩家侧攻击**：`PlayerAttackModule` + hitbox，让已有敌人 `Health` 真正挨打；弹反 / 无敌帧。
- **Enemy Prefab 向导**：`Tools/AI/Create Enemy Template` 一键装配（GroundSensor + Probe + Senses + Loco + Health + Brain + DepthLock）。
- **对话分支 / 条件 / 变量**：现在 `SimpleDialogueRunner` 只跑线性对白，需补分支与叙事变量接入 `NarrativeState`。
- **存档自动点**：`PlayerSavePoint` 接入触发器 + UI 反馈；跨场景状态序列化补完。
- **性能与体积**：URP Renderer Feature 层的体积光 / 大气（等 URP 官方或自写）；4K 下帧生成优化。
- **资产管线**：AssetGroups / Addressables 接入，为美术资源热加载铺路。
- **构建脚本**：Windows/PC 出包 CI。

---

## 二、美术部分（需要美术产出）

以下是**可以并行开工**的内容，程序侧框架都已就位，挂上去即可工作。

### 1. 角色（Player）

必需：
- **角色 Sprite / 骨骼**（2D Skeletal Animation 或帧动画均可）
- **动画状态**：`Idle`、`Run`、`RunTurn`（反向急停）、`RunStop`、`JumpStart`、`JumpLoop`、`Fall`、`Land`、`Dash`
- Pivot 约定：**脚底中心**
- 单位约定：**1 Unity 单位 = 角色身高 1/2**（后续微调，保证相机默认 FOV 30 + Ortho 6 下视觉合理）

材质要求：
- 材质使用 `Universal Render Pipeline / 2D / Sprite-Lit-Default`。
- 透明通道需干净，不带灰边。

### 2. 敌人 / NPC

- 每个敌人一份：`Idle` / `Move` / `Attack` / `Hurt` / `Dead`。
- NPC：`Idle` / `Talk`（简易嘴动或头部反应即可）。
- 与玩家同 Pivot / 单位规范。

### 3. 场景（2.5D 分层）

**Z 轴深度协议**（严格遵守，`Framework/ZDepth.cs` 里有常量）：

| 层级 | Z 值 | 用途 |
|---|---|---|
| Gameplay | `0` | 角色、敌人、可交互物、战斗碰撞 |
| Foreground Near | `-2` | 镜头前的叶片、栏杆等 |
| Foreground Far | `-5` | 近前景装饰 |
| Background Near | `10` | 近景建筑 |
| Background Mid | `30` | 中景 |
| Background Far | `80` | 远山 |
| Sky | `200` | 天空片 |

- 相机位于 `Z=-10`，FOV 30，聚焦在 `Z=0` 平面。
- **所有非 Gameplay 层的物体**挂 `Framework/DepthLock`，把 `targetZ` 设为对应数值，防漂移。
- 场景按"前景 / Gameplay / 背景近 / 中 / 远 / 天空"分 Hierarchy 父物体，方便统一管理。

需要产出：
- 每个关卡的**分层 Sprite 切片**（前景遮挡、主层装饰、各级背景、天空）。
- 可重复 tile 的地形 / 墙面贴图。
- 关卡**边界参考图**（给程序确定 `CameraBoundary` 的 Polygon 轮廓）。

### 4. UI

- 对白框、名字条、立绘（可选）、交互提示图标（如 `E` 键图）、黑场淡入贴图（可为纯色留空）。
- UI 锚点统一按 **1920×1080** 安全框出。
- 所有 UI 图集标记为 `Sprite (2D and UI)`，`Mesh Type = Full Rect`。

### 5. 光影与氛围（依赖 2.5D 透视）

- Sprite 材质全部换到 **Sprite-Lit**，场景里程序会放 3D `Light`（Point / Spot），美术只需保证：
  - Sprite 法线 / 自发光通道可用（如果要）。
  - 关键物件产出 **Normal Map**（可选，提升打光立体感）。
- 风格氛围图（Mood Board）对齐用，一场景至少一张。

### 6. 特效 / VFX

- 受击粒子、冲刺残影、落地尘土、环境粒子（光斑 / 雾气 / 落叶）。
- 材质使用 URP Particle Shader。

### 7. 音频资产（可以美术或独立音频）

- BGM：分场景 / 分状态（探索 / 战斗 / 过场）。
- SE：脚步（按地面类型分）、跳跃起/落、攻击挥空/命中、UI 点击、对白推进。
- 命名规范：`BGM_场景名_状态` / `SE_分类_具体名`。

### 8. 通用资产规范（重要，便于程序落库）

- **文件夹**：`Assets/Art/{Characters,Enemies,Environment,UI,VFX,Audio}/...`（美术自建，程序这边不强约束结构但请保持一致）。
- **命名**：英文 + 下划线，小写为主（`player_idle_01.png`）。
- **PNG**：Premultiplied Alpha 不推荐；普通 Straight Alpha。
- **动画**：给同一个角色的所有序列帧尺寸一致；Pivot 在同一相对位置。
- **交付形式**：直接进仓库（Git LFS 后续再议，目前项目资产量还小）。
- **场景 Prefab**：美术完成一个可用场景后打成 Prefab 或直接交付 `.unity`，程序负责挂 `DepthLock` / `CameraBoundary` / 触发器。

---

## 三、环境 / 依赖

- Unity 6（URP 17.0.4）
- com.unity.cinemachine 3.1.6（`Unity.Cinemachine` / `CinemachineCamera`）
- com.unity.inputsystem 1.13.1
- com.unity.feature.2d 2.0.1
- com.unity.timeline 1.8.7

Git 远端：`https://github.com/yishiyishiyishi/porject-.git`，主分支 `main`。
