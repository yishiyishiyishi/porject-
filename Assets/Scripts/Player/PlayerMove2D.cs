using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2D 角色水平移动控制器。
/// 适配自参考项目 DevelopAUnityActionGameIn5Min 的 PlayerMove + CharacterMove，
/// 去除了 Odin Inspector、Character 框架与状态/动画依赖，只保留核心手感：
///   - 加速度 / 刹车加速度分离
///   - 空中加速折损系数
///   - MoveTo 逼近目标速度
///   - 朝向翻转
///
/// 使用方式：
///   1. 在角色 GameObject 上挂 Rigidbody2D（Gravity Scale 按需设置）。
///   2. 挂 PlayerInput 组件，Actions 指向 Assets/InputSystem_Actions.inputactions，
///      Behavior 设为 "Send Messages"，Default Map 设为 "Player"。
///   3. 挂本脚本。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 12f;
    public float moveAcceleration = 50f;
    public float moveBrake = 100f;
    [Tooltip("空中时加速度的折损系数")]
    public float moveAccelerationAirFactor = 0.2f;

    [Header("Ground Check (可选)")]
    [Tooltip("勾选后使用 groundCheck 向下 OverlapCircle 判定接地；否则默认视为在地面。")]
    public bool useGroundCheck = false;
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer = ~0;

    [Header("Facing")]
    [Tooltip("当前朝向：1 为右，-1 为左。会根据输入自动更新。")]
    public int direction = 1;
    [Tooltip("是否通过翻转 localScale.x 表现朝向。")]
    public bool flipSpriteByScale = true;

    private Rigidbody2D _rb;
    private float _horizontalInput;

    public bool IsOnGround
    {
        get
        {
            if (!useGroundCheck) return true;
            if (groundCheck == null) return true;
            return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    // PlayerInput (Send Messages) 回调：Player/Move 绑定的 Vector2
    private void OnMove(InputValue value)
    {
        Vector2 v = value.Get<Vector2>();
        _horizontalInput = Mathf.Abs(v.x) < 0.01f ? 0f : Mathf.Sign(v.x);
    }

    private void FixedUpdate()
    {
        Vector2 velocity = _rb.linearVelocity;
        float moveInput = _horizontalInput;

        // 加速或减速：当输入方向与当前速度方向一致时用加速度，否则用刹车加速度
        float acceleration = moveBrake;
        if (moveInput * velocity.x > 0f)
            acceleration = moveAcceleration;

        // 空中加速度折损
        if (!IsOnGround)
            acceleration *= moveAccelerationAirFactor;

        velocity.x = velocity.x.MoveTo(moveInput * moveSpeed, acceleration * Time.fixedDeltaTime);

        // 角色朝向
        if (moveInput != 0f)
            SetDirection((int)moveInput);

        _rb.linearVelocity = velocity;
    }

    public void SetDirection(int dir)
    {
        if (dir == 0 || dir == direction) return;
        direction = dir > 0 ? 1 : -1;
        if (flipSpriteByScale)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * direction;
            transform.localScale = s;
        }
    }
}
