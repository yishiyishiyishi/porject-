# GAME1

Unity 2D 动作 / 叙事类游戏工程。基于 Unity 6 + URP 2D + 新 Input System + Cinemachine 3.x。

## 环境 / 主要依赖

- Unity 6（URP 17.0.4）
- com.unity.cinemachine 3.1.6（命名空间 `Unity.Cinemachine`，组件为 `CinemachineCamera`）
- com.unity.inputsystem 1.13.1
- com.unity.feature.2d 2.0.1
- com.unity.timeline 1.8.7

所有 Unity 自动生成物（`Library/`、`Temp/`、`Logs/`、`Obj/`、`UserSettings/`、各类 `.csproj/.sln`）都在 `.gitignore` 中忽略，仓库里只保留 `Assets/`、`Packages/`、`ProjectSettings/`。

## 目录结构

```
Assets/
  Scenes/                           主场景
  Settings/                         URP 渲染管线资产
  InputSystem_Actions.inputactions  输入动作表
  Scripts/
    MathTools.cs                    通用数值 / 向量工具（MoveTo 等扩展方法）
    Core/
      EventBus.cs                   全局事件总线
      Narrative/                    叙事核心层
      UI/                           UI 核心层
    Framework/
      Actor/                        Actor 基类与通用行为
      Camera/
        CameraManager.cs            相机总控（切换 / 锁定 / 震屏 / 镜头平滑）
      Dialogue/                     对话系统
      Input/                        输入封装
      Interaction/                  交互触发
      Sensors/                      环境感知（接地 / 碰墙等）
      NarrativeRuntime.cs           叙事运行时入口
    Player/
      PlayerActor.cs                玩家 Actor
      DialoguePlayerFreeze.cs       对话时冻结玩家控制
      Data/                         玩家相关数据
      Modules/
        MoveModule.cs               移动模块
        JumpModule.cs               跳跃模块
        DashModule.cs               冲刺模块
```

## 当前进度

已完成：

- 工程初始化并接入远端 `yishiyishiyishi/porject-`，Unity 专用 `.gitignore` 配好。
- 输入层：使用新 Input System + `InputSystem_Actions` 资产，在 `Framework/Input` 下做了封装。
- 角色层：`PlayerActor` + 三个能力模块（Move / Jump / Dash）。
- 叙事层：`Core/Narrative`、`Framework/Dialogue`、`NarrativeRuntime`、`DialoguePlayerFreeze`（对话时冻结玩家）搭建完毕。
- 基础设施：`EventBus` 事件总线、`MathTools` 数值工具、`Sensors` 环境感知、`Interaction` 交互触发。
- 相机层：
  - 主相机挂 `Cinemachine Brain`，场景里跟随虚拟相机已绑定玩家。
  - `Framework/Camera/CameraManager.cs` 提供统一控制入口（见下）。

进行中 / 待办：

- 在场景里补齐 `CameraBoundary`（Polygon Collider 2D）并挂 `CinemachineConfiner2D` 到主 vcam。
- 为主 vcam 的 `CinemachinePositionComposer` 调参（Dead Zone / Damping）。
- 场景触发器 + 切换相机：在关键转场点放 Trigger，通过 `CameraManager.SwitchTo(key)` 升 / 降优先级。
- 震屏：给需要震动的 vcam 加 `CinemachineImpulseListener`，在攻击 / 落地 / 受击处调用 `CameraManager.Shake`。
- 后期：URP Global Volume，启用 Bloom 与 Vignette。

## CameraManager 用法速览

命名空间 `Framework.Cameras`，单例 `CameraManager.Instance`。

- `SwitchTo(string key)`：把指定具名 vcam 的 Priority 升到 `activePriority`，其它复位为 0，`Cinemachine Brain` 自动做平滑切换。传 `null` 回到主相机。
- `LockCameraPosition(bool)`：启用 / 禁用主 vcam 的跟随组件，用于演出锁镜头。
- `SetOrthoSize(size, duration)`：协程平滑修改正交尺寸，SmoothStep 插值，使用 `unscaledDeltaTime`，演出暂停时仍能过渡。
- `SetFollowOffset(offset, duration)`：平滑修改 `CinemachineFollow.FollowOffset`（若用 `CinemachinePositionComposer` 则改用其 `TargetOffset`）。
- `Shake(Vector3 velocity)` / `ShakeQuick(float strength)`：通过 `CinemachineImpulseSource` 触发震屏；默认 source 不存在时会自动添加到 `CameraManager` 所在对象。

### 场景里挂载步骤

1. 新建空物体 `CameraManager`，挂本脚本。
2. 把主跟随 vcam 拖进 `Main Vcam`，填一个默认 `Main Priority`（例如 10）、`Active Priority`（例如 20）。
3. 在 `Cameras` 列表里按 `key + CinemachineCamera` 注册所有可切换相机（如 `Corridor`、`Boss`、`Cutscene_Bridge`）。
4. 需要震屏的地方调用 `CameraManager.Instance.ShakeQuick(0.5f)`；相应的 vcam 需手动添加 `CinemachineImpulseListener` 扩展。
5. 剧情 / 触发器内调用 `SwitchTo("Corridor")` 切镜头，结束时 `SwitchTo(null)`。

## Git

远端：`https://github.com/yishiyishiyishi/porject-.git`，主分支 `main`。
