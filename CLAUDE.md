# CLAUDE.md

本项目的完整上下文。下次打开直接读这个文件即可恢复记忆。

---

## 项目定位

- **Unity 6 + URP 17.0.4 + 新 Input System + Cinemachine 3.1.6**，2.5D（透视相机 + 2D Sprite）
- 商业向独立游戏，参考尼尔（Nier）：meta / 第四面墙 / 多结局 / 循环叙事
- 仓库：`https://github.com/yishiyishiyishi/porject-.git`，主分支 `main`
- 本地路径：`D:/Project/GAME/GAME1`
- 分工：用户 = 唯一程序；另有美术朋友
- 参考项目：`https://github.com/Kurassier/DevelopAUnityActionGameIn5Min.git`（已剥离 Odin 付费依赖，自研 Actor 框架替代其 Character 框架）

---

## 已搭建的系统（程序侧）

### Core 层
- `Core/EventBus.cs` —— 泛型事件总线，`Subscribe<T>/Publish<T>`，域重载时 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 重置
- `Core/Narrative/NarrativeState.cs` —— ScriptableObject，字段：`loopIndex`、`totalBoots`、`glitchDiscovered`、`reachedMilestones`；`MarkMilestone()` 发 `NarrativeMilestoneReached`；`BeginNewLoop()` 发 `LoopStarted`
- `Core/UI/IMetaUIReactor.cs` —— UI 腐蚀/故障效果接口

### Framework/Actor（自研角色框架）
- `ActorState` —— POCO，持有 Rigidbody2D 引用，`Velocity` 代理 `linearVelocity`、`Direction`、`IsGrounded`、`IsTouchingWall`、`LocalTimeScale`
- `ActionGate` —— `[Flags] ActionTag { None, Move, Jump, Dash, Attack, Damage, All }`；`Block(tag, duration)` / `IsBlocked(tag)` / `Tick(dt)`
- `ActorModule` —— 接口 `IActorModule`（Order、OnAttach、Tick、FixedTick）+ 抽象基类 `ActorModule : MonoBehaviour`
- `Actor` —— 按 Order 驱动模块，持有 State+Gate，发 `ActorSpawned`/`ActorDespawned`，`IsPaused` 闸门、`SetDirection()` 翻 localScale.x、`GetModule<T>()`

### Framework/Input
- `IPlayerInput` —— `Horizontal`、`JumpHeld`、`JumpPressedAt/DashPressedAt/AttackPressedAt/InteractPressedAt`、`ConsumeJump/Dash/Attack/Interact`
- `PlayerInputReader` —— Send Messages 回调（OnMove/OnJump/OnSprint/OnAttack/OnInteract），`NeverPressed = -999f` 哨兵

### Framework/Sensors
- `GroundSensor` —— Order=-100，`Physics2D.OverlapCircle` 写 `State.IsGrounded`

### Framework/Interaction
- `Interactable` —— 抽象 MB，`[RequireComponent(Collider2D)]`，字段：prompt、requireMilestone、consumeOnMilestone
- `InteractorModule` —— Order=50，`Physics2D.OverlapCircleNonAlloc`，**通过 GetComponent<IPlayerInput> 而非 PlayerActor 强转**（防止 Framework 反向依赖 Player 命名空间），发 `InteractCandidateChanged`/`InteractPerformed`

### Framework/Dialogue
- `IDialogueRunner` —— 接口（IsRunning、StartDialogue、Advance、Choose、Stop）+ 事件 DialogueStarted/LineShown/Ended
- `DialogueLine` —— SO，speaker、text、markMilestoneOnShow、next、`List<Choice>{text, target, requireMilestone}`
- `SimpleDialogueRunner` —— Instance 单例，目前只跑线性对白，按里程碑过滤选项
- `DialogueTrigger` —— 继承 Interactable
- `DialogueAdvancer` —— 临时调试（E/Space/Enter 推进，1-4 选择）

### Framework/NarrativeRuntime
- `[DefaultExecutionOrder(-1000)]`，静态 `State` 访问器，订阅 `MetaLoaded` 同步 totalBoots/glitchDiscovered，`resetSlotFieldsOnAwake` 清 SO 编辑器残留

### Framework/Camera（2.5D）
- `CameraManager` 单例：`SwitchTo(key)`、`LockCameraPosition(bool)`、`SetOrthoSize`/`SetFollowOffset`、`Shake`/`ShakeQuick`
- `CameraTrigger`：进入切相机、离开回主相机
- `DepthLock` + `ZDepth`：Z 轴协议常量
- Editor 向导：`Assets/Scripts/Editor/CameraSetupWizard.cs`（菜单 `Tools/Camera/*`），**Cinemachine 3.x 的字段已修：`composer.Composition.DeadZone/HardLimits/ScreenPosition`**（用户外部改过一次，保留其改动）

### Framework/Save（两层存档，meta 跨槽位持久化）
- `SlotSaveData` —— schemaVersion、slotIndex、timestampIso、sceneName、savePointId、loopIndex、reachedMilestones、blobs
- `MetaSaveData` —— totalBoots、totalDeaths、glitchDiscovered、endingsReached、persistentFlags、boot 时间戳
- `SlotHeader`（槽位列表轻量版）+ `BlobEntry{id, json}`（GetBlob/SetBlob）
- `SaveEvents` —— SaveCaptureRequested/RestoreRequested、SlotSaved/Loaded、MetaLoaded/Saved
- `SaveManager` —— 单例，`Application.persistentDataPath/Saves/`，Awake 里加载 meta + `totalBoots++` + 发 MetaLoaded；**原子写入 via .tmp + File.Move**；`SaveSlot/LoadSlot/DeleteSlot/ListSlots`，静态 `WriteBlob<T>/ReadBlob<T>`
  - **WipeSlotsOnly**（玩家向删档）：只删 slot_*.json，Meta 保留并累加 `slotWipeCount` + `lastSlotWipeIso` → 发 `SlotsWiped` 事件。这是尼尔式"系统记得你删过档"机制的入口
  - **WipeAll**（开发者向）：清一切含 Meta，仅保留 totalBoots
- `SaveDebugKeys` —— F5 存、F9 读、F8 擦
- **Blob 模式**：各模块通过 EventBus 订阅 SaveCaptureRequested/RestoreRequested 自己贡献数据（如 `PlayerSavePoint` 序列化位置+朝向）

### Framework/UI（栈式 + 自动暂停）
- `UIEvents` —— UIPushed/Popped、UIPauseStateChanged
- `UIView` —— `[RequireComponent(Canvas)]`，pausesGame、consumesEscape、baseSortingOrder、viewId；virtual OnPushed/Popped/Covered/Revealed
- `UIManager` —— 单例，`List<UIView>` 栈 + Dictionary 注册表，Push/Pop/PopUntil/PopAll、Get<T>()、ESC 处理、自动 `Time.timeScale` 暂停+恢复
- Views：`InteractPromptView`（非模态、订阅 InteractCandidateChanged）、`DialogueView`（自动 Push/Pop）、`FadeView`（Instance 单例、CanvasGroup 用 unscaledDeltaTime、sortingOrder=10000）、`PlayerHealthView`（HP HUD；Assign(Health) 或按 Tag=Player 自动找；白条延迟追平；订阅 ActorSpawned 兜底晚注册）

### Framework/Audio
- `AudioCue` SO —— clips[]、volume/pitch jitter、spatialBlend、priority、maxConcurrent
- `MusicTrack` SO —— clip、targetVolume、defaultFadeIn/Out、loop、loopStartSeconds
- `AudioFilterPreset` SO —— lowpass/highpass/distortion 参数 + musicVolumeScale + transitionTime；`AudioFilterDefaults.Passthrough`
- `AudioManager` —— 单例，**SFX 池 16 个 AudioSource + 双音轨 A/B 交叉淡入淡出**，滤镜挂在 manager GO 上，ApplyFilter/ClearFilter 平滑过渡（meta "hack 模式"用）

### Framework/Scene（异步 + 淡场）
- `SceneSpawnPoint` —— spawnId + facing + gizmo
- `LevelLoadEvents` —— LevelLoadRequested/Activated/LoadCompleted
- `LevelLoader` —— 单例，**流程：FadeOut → (可选) AutoSave → LoadSceneAsync(allowSceneActivation=false 卡 0.9) → 激活 → 等一帧 → Teleport 到 SpawnPoint → FadeIn**
- `LevelPortal` —— Interactable 子类，交互切场景

### Framework/Visual（纸片人防假 + 手感反馈）
- `DynamicShadow`：脚下椭圆影子 SpriteRenderer，向下 Raycast 找地面，按"离地高度"调整 scale/alpha。横板跳跃也受益；TopDown 主要救命道具。
- `SquashStretch`：挤压/拉伸。visualTarget 子物体（不改 Actor 根 scale 免影响碰撞），`TriggerJump / TriggerLand(impact01) / TriggerTurn / Push(xyScale)`，指数回弹。保留 localScale.x 方向（防止方向翻转被覆盖）。
- `YSortModule`：Y 越小 sortingOrder 越大，脚下挡远处。TopDown 必开，横板可关。
- `JumpModule` 自动检测 `SquashStretch`：起跳 TriggerJump、落地按 _peakFallSpeed 估 impact 触发 TriggerLand（空中累计最低 Y 速度，落地清零）

### Framework/View（尼尔式视角切换）
- `ViewMode` 枚举：Side（横版）/ TopDown（俯视）
- `ViewModeController` 单例，`static Current` 可供任意模块读；`SetMode(mode)` 切换，同时可按配置联动 CameraManager 切相机
- `ViewModeTrigger`（Collider2D Trigger）进入 → SetMode(TopDown)，离开 → 回 Side
- 事件 `ViewModeChanged` 广播到 EventBus
- **物理仍旧是 Rigidbody2D（方案 A "伪俯视"）**：TopDown 下 JumpModule 早退，MoveModule 改吃 Horizontal+Vertical 驱动 XY，2D 骨骼不会被压成线

### Framework/Combat（玩家侧攻击打通）
- `Faction` 枚举（Player/Enemy/Neutral/None）+ `IsHostile` 扩展
- `Hurtbox`（挂在可被打的对象上，含 faction + IDamageable 引用）
- `HitboxQuery.OverlapBox(center, size, angle, mask, attackerFaction, template, attacker, alreadyHit)`：攻击方在 active 帧调，内部 OverlapBoxNonAlloc，过滤同阵营 + 跨帧去重，对目标 TakeDamage 并发 `HitLanded` 事件
- `HurtFlash`：订阅 sibling Health.OnDamaged，SpriteRenderer 闪白；unscaledDeltaTime 计时确保顿帧期间也看到闪白；敌我通用
- `Player/PlayerAttackModule`：3 段 windup/active/recovery × N 段连招，输入缓冲接招，active 段每 Tick 重判（支持快速穿过），命中自动顿帧 + 轻震
- **EnemyBrain 攻击统一 HitboxQuery**：圆形 hitbox 近似方形（2r × 2r），跨帧去重；字段 `attackerFaction` 默认 Enemy

### Framework/Time（顿帧）
- `HitStop.Pulse(duration, freezeScale=0.01f)`：隐藏 DontDestroyOnLoad Driver 协程，unscaledDeltaTime 计时，把 Time.timeScale 拉近 0 再恢复，配合 Actor `LocalTimeScale` 做个体 / 全局时间控制

### Framework/Pooling（对象池）
- `IPoolable`（可选 OnAcquire/OnRelease）
- `ObjectPool<T : Component>`：prefab + prewarm + maxSize，Stack 内部，超上限自动销毁
- `PoolManager` 单例，key = (Type, prefab InstanceID)，`PoolManager.Get<Bullet>(prefab).Acquire(pos, rot)`
- 子弹弹幕（未来尼尔 Pod 射击）不再 Instantiate/Destroy 爆 GC

### Framework/Audio（+ 事件 Facade）
- 新增 `AudioEvents.PlaySfx / PlayMusic / ApplyFilter / ClearFilter`，业务层走 EventBus 发，不持 AudioManager 引用
- AudioManager 在 Awake 订阅、OnDestroy 退订，无 AudioManager 场景静默失败

### Framework/AI（敌人 + NPC）
- `StateMachine<T>` —— 极简泛型 FSM，`Configure().OnEnter/Tick/FixedTick/Exit`，`TimeInState`、`ReenterState()`（连招用）、`OnTransition` 事件
- `Health` + `IDamageable` + `DamageInfo` —— 通用血量模块（敌我共用），击退覆写 `linearVelocity`，派发 `ActorDamaged`/`ActorDied`
- `EnvironmentProbe` —— 前方墙 + 脚下悬崖双 Raycast，`RaycastNonAlloc` + 自身 Rigidbody 过滤防自击
- `EnemySenses` —— Tag 查玩家，距离/视野/视线遮挡，迟滞环防抖（detection/loseRadius）
- `EnemyLocomotion` —— `Request(dir, speed)` / `Stop()`，**指令持久化**（指令保持到下一条，不按帧清零，修复了 Tick vs FixedTick 帧率不匹配的抖脚 bug）
- `EnemyBrain` —— `Patrol/Chase/Attack/Stunned/Dead`，攻击 windup/active/recovery 三段 + `OverlapCircle` 命中，连招走 `FSM.ReenterState()`
- `EnemyActor` —— Actor 子类 + faction
- `NpcWanderer` —— NPC Idle/Wander/Talk，`SetTalking(bool)` 对接对话

### Player
- `PlayerActor` —— 继承 Actor，GetComponent 缓存 IPlayerInput
- `MoveModule` —— Order=10，accel/brake/airFactor，尊重 `ActionTag.Move` 阻塞
- `JumpModule` —— Order=20，**jumpSpeed=20、分段重力（Up=60 / Float=40 / Fall=70、阈值=2）、coyoteTime、jumpBufferTime、jumpCutSpeed 可变跳跃**；**Rigidbody2D.gravityScale 必须为 0**（模块自己算重力）
- `DashModule` —— Order=30，用 `Displacement` SO 驱动 AnimationCurve；发 DashStarted/Ended
- `Displacement` SO —— maxSpeed、length、AnimationCurve、zeroGravity
- `DialoguePlayerFreeze` —— 订阅 DialogueStarted/Ended 切 Actor.IsPaused
- `PlayerSavePoint` —— Blob 贡献玩家位置+朝向

---

## 关键技术决策与踩坑

1. **Odin 付费插件**：移除 `[ReadOnly]`/`[Button]` 属性（只是编辑器装饰）
2. **Character → Actor 替换**：用 `[Flags] ActionGate` 替代参考项目的字符串 `ActionIgnoreTag`
3. **InteractorModule 不能强转 PlayerActor**：Framework 不得反向依赖 Player 命名空间，用 `GetComponent<IPlayerInput>()`
4. **Cinemachine 3.x 迁移**：`composer.DeadZone` → `composer.Composition.DeadZone`（HardLimits、ScreenPosition 同理）
5. **两层存档**：SlotSaveData（每循环）+ MetaSaveData（跨运行）——"系统记得你删过档" 的 meta 叙事机制
6. **Blob 模式**：模块通过 EventBus 自己塞自己的存档段，SaveManager 不需要知道业务细节
7. **原子写入**：`.tmp + File.Move` 防崩溃中途损档；每个数据结构带 `schemaVersion` 方便未来迁移
8. **Dialogue 不锁供应商**：`IDialogueRunner` 抽象，将来接 Ink 只需新实现
9. **3D vs 2D 冲突**：胶囊必须用 `2D Object > Sprites > Capsule`，不能用 3D Capsule（Mesh Filter + 3D Collider 与 Rigidbody2D 不兼容）
10. **LocalTimeScale**：Actor 自己有一份，用于个体时间膨胀（而不是全局 Time.timeScale）

---

## Z 轴深度协议（美术必须遵守）

| 层 | Z | 用途 |
|---|---|---|
| Gameplay | 0 | 角色/敌人/可交互/战斗 |
| Foreground Near | -2 | 前景叶片栏杆 |
| Foreground Far | -5 | 近前景装饰 |
| Background Near | 10 | 近景建筑 |
| Background Mid | 30 | 中景 |
| Background Far | 80 | 远山 |
| Sky | 200 | 天空片 |

相机 Z=-10，FOV 30，聚焦 Z=0 平面。非 Gameplay 层挂 `Framework/DepthLock`。

---

## 待做

- 相机触发网（关键转场/Boss 房/剧情机位布点）
- **TopDown 项目当前优先级降级**：主力横板，TopDown 框架就位但暂不接入；3/4 视角美术 + 脚尘粒子 + 法线贴图那批等横板成型后再回来做
- **TopDown 相机预设**：在 CameraManager 注册一个俯视 CinemachineCamera（key 如 "TopDown"），填到 `ViewModeController.topDownCameraKey`
- **给敌人 + Player Hurtbox**：场景 / prefab 挂 `Hurtbox`(faction + damageable=Health)，敌我才能真正打到对方
- 无敌帧 / 弹反
- 对话分支/条件/变量（现在只线性）
- SavePoint 触发器 + UI 反馈、跨场景状态序列化补完
- URP 体积光/大气
- AssetGroups / Addressables
- Windows 出包 CI
- 背景正经资产（bg1/2/3 占位图重做）

**下一个方向候选**（用户尚未决定）：FMOD 音频中间件 / TextMeshPro UI 升级 / 暂停菜单

---

## AI 模块审计修复（2026-04-21 第二轮）

1. **EnemyLocomotion 抖脚** —— `_hasRequestThisTick` 在 FixedTick 里自动清零，帧率低时一个 Tick 周期内多次 FixedTick 会把 input 清成 0 → 敌人走走停停。改为 **指令持久化**（Request/Stop 之间保持不变）
2. **EnemyBrain 连招失效** —— `ChangeState(Attack)` 当前=Attack 时因 equal 检查被吞掉。新加 `StateMachine.ReenterState()`，TickAttack 末段连招改走 ReenterState
3. **EnemyBrain `_attackFired` 泄漏** —— 硬直打断攻击后 `_attackFired=true` 没复位，再次进入 Attack 会跳过 active 帧。改为在 Attack.OnEnter 复位
4. **EnemyBrain TickChase 丢目标不停步** —— `if (Target == null) return;` 早返回但上一条 Request 还在，敌人会朝空气走。加 `_loco.Stop()`
5. **EnemyBrain TickPatrol 用 Time.deltaTime** —— 无视 Actor.LocalTimeScale，未来做命中定格 / 时间膨胀时敌人不会随之减速。改用 FSM 传入的 dt
6. **EnvironmentProbe 自击** —— `Physics2D.Raycast` 默认受 `queriesStartInColliders` 影响，射线起点在自身 Collider 内会打中自己。改用 `RaycastNonAlloc` + 过滤 `rigidbody == State.Rb`

---

## 已做的代码审计修复（2026-04-21 第一轮）

1. **EventBus.Publish** 改为 `GetInvocationList()` 逐个 try/catch —— 单订阅者异常不再级联掐断整条事件链
2. **NarrativeRuntime** 单例改用独立 `_instance` 字段严格判定 —— 之前 `State != stateAsset` 判式在同一 SO 重复 Awake 时会放行，导致 `Subscribe<MetaLoaded>` 重复订阅、事件二次触发
3. **SaveManager.ReadBlob** 的 `catch { return null; }` 改为带日志的 catch，腐坏 blob 不再静默
4. **AudioManager.ApplyFilter** 把 `_currentPreset = preset` 移到 null 替换之后 —— 之前传 null 会让 `_currentPreset` 永久为 null
5. **AudioManager.MoveFiltersTo** 删除（空方法 + 一段说明注释），说明保留为代码注释
6. **Player 模块 OnAttach** `(PlayerActor)actor` 强转改为 `as + null 断言`，FixedTick 头部 null 短路，防止误挂到非 Player Actor 时的神秘崩溃

---

## 协作风格偏好

- 用户是经验丰富的程序，直接给代码、不要过度解释
- 简体中文回应
- 短回复优先，别啰嗦
- 做商业级品质：原子写、schemaVersion、接口抽象、无供应商锁定
- 架构洁癖：Framework 不反向依赖 Player/业务层
