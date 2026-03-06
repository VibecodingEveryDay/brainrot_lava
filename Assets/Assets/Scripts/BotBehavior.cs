using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Поведение бота: выбор случайного брейнрота → бег к нему → взятие в руки → перенос до StartFinish → удаление (в зоне).
/// Использует CharacterController, гравитацию и проверку земли как у игрока (ступеньки).
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(BotCarryController))]
public class BotBehavior : MonoBehaviour
{
    public enum State
    {
        GoingToBrainrot,
        CarryingToStartFinish
    }
    
    [Header("Движение")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float rotationSpeed = 10f;
    
    [Header("Character Controller (ступеньки)")]
    [Tooltip("Максимальная высота ступеньки, на которую бот может автоматически подняться (м). 0 = не менять настройку с префаба. Если бот застревает на ступеньках — задать не меньше высоты ступени (например 0.4–0.5).")]
    [SerializeField] private float stepOffsetOverride = 0.5f;
    
    [Header("Knockback (отталкивание мячом)")]
    [Tooltip("Скорость затухания отталкивания")]
    [SerializeField] private float knockbackDecay = 5f;
    
    [Header("Проверка земли (ступеньки)")]
    [SerializeField] private float groundCheckLength = 0.7f;
    [SerializeField] private float minGroundNormalY = 0.35f;
    [SerializeField] private float groundCheckRadius = 0.2f;
    
    [Header("Время жизни")]
    [Tooltip("Через сколько секунд после появления бот и брейнрот в руках удаляются")]
    [SerializeField] private float lifetimeSeconds = 60f;
    
    [Header("Цели")]
    [Tooltip("Дистанция по XZ, при которой бот считает, что дошёл до брейнрота")]
    [SerializeField] private float takeDistanceThreshold = 1.5f;
    [Tooltip("Интервал перевыбора цели (сек), если брейнрот ещё не взят")]
    [SerializeField] private float retargetInterval = 2f;
    
    [Header("Ссылки")]
    [SerializeField] private Animator animator;
    [Tooltip("Дочерний объект с моделью (для привязки к капсуле). Если не задан — ищется по Animator.")]
    [SerializeField] private Transform modelTransform;
    [Tooltip("Смещение от пивота модели до низа (ног). 0 = пивот в ногах. Если пивот в центре/бедрах — задать расстояние до земли (например 0.9).")]
    [SerializeField] private float modelPivotToBottomOffset = 0f;
    
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsTakingHash = Animator.StringToHash("IsTaking");
    
    private CharacterController characterController;
    private BotCarryController carryController;
    private Vector3 velocity;
    private Vector3 knockbackVelocity = Vector3.zero;
    private Coroutine destroyAfterDelayCoroutine;
    private Coroutine lifetimeCoroutine;
    private State state = State.GoingToBrainrot;
    private BrainrotObject targetBrainrot;
    private Transform startFinishTarget;
    private float lastRetargetTime;
    private Vector3 modelLocalPositionInitial;
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        carryController = GetComponent<BotCarryController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (modelTransform == null && animator != null)
            modelTransform = animator.transform;
        // Отключаем Apply Root Motion, чтобы анимации не сдвигали модель относительно коллайдера
        if (animator != null)
            animator.applyRootMotion = false;
        // Step Offset: чтобы бот мог перешагивать ступеньки (иначе застревает при 0.3 и ниже)
        if (characterController != null && stepOffsetOverride > 0f)
            characterController.stepOffset = stepOffsetOverride;
    }
    
    private void Start()
    {
        if (modelTransform != null && modelTransform.parent == transform)
            modelLocalPositionInitial = modelTransform.localPosition;
        lastRetargetTime = Time.time;
        PickTargetBrainrot();
        CacheStartFinish();
        if (lifetimeSeconds > 0f)
            lifetimeCoroutine = StartCoroutine(DestroyAfterLifetimeCoroutine(lifetimeSeconds));
    }
    
    private void Update()
    {
        if (characterController == null || !characterController.enabled) return;
        
        bool isGrounded = CheckGrounded();
        ApplyGravity(isGrounded);
        
        if (state == State.GoingToBrainrot)
        {
            if (targetBrainrot == null || targetBrainrot.IsCarried() || PlacementPanel.IsBrainrotPlacedOnPanel(targetBrainrot))
            {
                if (Time.time - lastRetargetTime >= retargetInterval)
                {
                    PickTargetBrainrot();
                    lastRetargetTime = Time.time;
                }
            }
            
            if (targetBrainrot != null && !targetBrainrot.IsCarried() && !PlacementPanel.IsBrainrotPlacedOnPanel(targetBrainrot))
            {
                Vector3 toTarget = targetBrainrot.transform.position - transform.position;
                toTarget.y = 0f;
                float distXZ = toTarget.magnitude;
                
                if (distXZ <= takeDistanceThreshold)
                {
                    targetBrainrot.TakeBy(carryController);
                    state = State.CarryingToStartFinish;
                    CacheStartFinish();
                }
                else
                {
                    Vector3 moveDir = toTarget.normalized;
                    MoveAndRotate(moveDir);
                }
            }
        }
        else if (state == State.CarryingToStartFinish)
        {
            if (startFinishTarget == null)
                CacheStartFinish();
            
            if (startFinishTarget != null)
            {
                Vector3 toTarget = startFinishTarget.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Vector3 moveDir = toTarget.normalized;
                    MoveAndRotate(moveDir);
                }
            }
        }
        
        UpdateAnimator(isGrounded);
    }
    
    private void LateUpdate()
    {
        // Привязываем низ модельки к низу капсулы CharacterController
        if (modelTransform != null && modelTransform.parent == transform && characterController != null)
        {
            float capsuleBottomLocalY = characterController.center.y - characterController.height * 0.5f;
            float modelY = capsuleBottomLocalY + modelPivotToBottomOffset;
            modelTransform.localPosition = new Vector3(modelLocalPositionInitial.x, modelY, modelLocalPositionInitial.z);
        }
    }
    
    private bool CheckGrounded()
    {
        if (characterController.isGrounded && velocity.y <= 2f)
            return true;
        
        Vector3 bottom = transform.position + characterController.center + Vector3.down * (characterController.height * 0.5f);
        int layerMask = ~0;
        int wallBallLayer = LayerMask.NameToLayer("WallBall");
        if (wallBallLayer >= 0)
            layerMask &= ~(1 << wallBallLayer);
        
        if (groundCheckRadius > 0.001f && Physics.SphereCast(bottom, groundCheckRadius, Vector3.down, out RaycastHit sh, groundCheckLength, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (sh.collider != null && sh.collider.gameObject != gameObject && !sh.collider.isTrigger && sh.normal.y >= minGroundNormalY)
                return true;
        }
        
        Vector3 fwd = transform.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
        Vector3 rgt = transform.right; rgt.y = 0f; if (rgt.sqrMagnitude < 0.01f) rgt = Vector3.right; rgt.Normalize();
        Vector3[] origins = { bottom, bottom + fwd * groundCheckRadius, bottom - fwd * groundCheckRadius, bottom + rgt * groundCheckRadius, bottom - rgt * groundCheckRadius };
        for (int i = 0; i < origins.Length; i++)
        {
            if (Physics.Raycast(origins[i], Vector3.down, out RaycastHit hit, groundCheckLength, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.normal.y >= minGroundNormalY)
                    return true;
            }
        }
        
        return false;
    }
    
    private void ApplyGravity(bool isGrounded)
    {
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;
        
        characterController.Move(velocity * Time.deltaTime);
        
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            characterController.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Вызывается при ударе мячом: отталкивание в -Z и удаление бота через заданное время.
    /// </summary>
    public void OnHitByBall(float pushForce, float destroyDelaySeconds = 2f)
    {
        knockbackVelocity += Vector3.back * pushForce;
        if (destroyAfterDelayCoroutine != null)
            StopCoroutine(destroyAfterDelayCoroutine);
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
        destroyAfterDelayCoroutine = StartCoroutine(DestroyAfterDelayCoroutine(destroyDelaySeconds));
    }
    
    private IEnumerator DestroyAfterLifetimeCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        lifetimeCoroutine = null;
        DestroyBotAndCarried();
    }
    
    private IEnumerator DestroyAfterDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        destroyAfterDelayCoroutine = null;
        DestroyBotAndCarried();
    }
    
    /// <summary>
    /// Удаляет бота и брейнрот в его руках (если есть).
    /// </summary>
    private void DestroyBotAndCarried()
    {
        BrainrotObject carried = carryController != null ? carryController.GetCurrentCarriedObject() : null;
        if (carryController != null)
            carryController.DropObject();
        if (carried != null && carried.gameObject != null)
            Destroy(carried.gameObject);
        Destroy(gameObject);
    }
    
    private void MoveAndRotate(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.01f) return;
        
        characterController.Move(moveDir * moveSpeed * Time.deltaTime);
        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    
    private void PickTargetBrainrot()
    {
        BrainrotObject[] all = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        var pickable = new List<BrainrotObject>();
        for (int i = 0; i < all.Length; i++)
        {
            BrainrotObject br = all[i];
            if (br == null || br.IsCarried() || PlacementPanel.IsBrainrotPlacedOnPanel(br)) continue;
            pickable.Add(br);
        }
        
        if (pickable.Count > 0)
            targetBrainrot = pickable[Random.Range(0, pickable.Count)];
        else
            targetBrainrot = null;
    }
    
    private void CacheStartFinish()
    {
        StartFinishZone zone = FindFirstObjectByType<StartFinishZone>();
        startFinishTarget = zone != null ? zone.transform : null;
    }
    
    private void UpdateAnimator(bool isGrounded)
    {
        if (animator == null) return;
        
        float speed = (state == State.GoingToBrainrot && targetBrainrot != null) || (state == State.CarryingToStartFinish && startFinishTarget != null)
            ? moveSpeed : 0f;
        animator.SetFloat(SpeedHash, speed);
        // Бот не прыгает — для аниматора всегда считаем приземлённым, чтобы не переключалось на полёт/падение
        animator.SetBool(IsGroundedHash, true);
        animator.SetBool(IsTakingHash, carryController != null && carryController.GetCurrentCarriedObject() != null);
    }
}
