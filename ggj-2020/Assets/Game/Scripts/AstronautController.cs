using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public enum AstronautEmote
{
  Attack = 0,
  HitReact,
}

public enum AstronautIdle
{
  Idle = 0,
  Move,
  Stunned,
  Panic
}

public class AstronautController : MonoBehaviour
{
  public static IReadOnlyList<AstronautController> Instances => _instances;

  public event System.Action Died;
  public event System.Action Despawned;

  public Vector3 MoveVector
  {
    get { return _moveVector; }
    set
    {
      _moveVector = Vector3.ClampMagnitude(value, 1);
    }
  }

  public RoomInhabitantComponent RoomInhabitant => _roomInhabitant;
  public bool IsStunned => _stunTimer > 0;

  [SerializeField]
  private RoomInhabitantComponent _roomInhabitant = null;

  [SerializeField]
  private BatteryComponent _batteryComponent = null;

  [SerializeField]
  private Rigidbody _rb = null;

  [SerializeField]
  private Animator _animator = null;

  [SerializeField]
  private Transform _visualRoot = null;

  [SerializeField]
  private GameObject[] _hideOnDie = null;

  [SerializeField]
  private GameObject[] _showOnDie = null;

  [SerializeField]
  private GameObject _stunFx = null;

  [SerializeField]
  private GameObject _spawnFxPrefab = null;

  [SerializeField]
  private GameObject _attackFxPrefab = null;

  [SerializeField]
  private GameObject[] _headPrefabs = null;

  [SerializeField]
  private Transform _headSpawnRoot = null;

  [SerializeField]
  private GameObject _exclamationEffect = null;

  [SerializeField]
  private float _acceleration = 10;

  [SerializeField]
  private float _maxSpeed = 1;

  [SerializeField]
  private float _attackCooldown = 1;

  [SerializeField]
  private bool _canStun = true;

  [SerializeField]
  private SoundBank _hitSound = null;

  [SerializeField]
  private SoundBank _deathSound = null;

  private Vector3 _moveVector;
  private float _zRot;
  private float _attackCooldownTimer;
  private float _idleBlend;
  private AstronautIdle _currentIdleState;
  private bool _isDead;
  private bool _isColliding;
  private float _stunTimer;
  private float _deathTimer;
  private GameObject _headObj;

  private static List<AstronautController> _instances = new List<AstronautController>();

  private static readonly int kAnimIdleState = Animator.StringToHash("IdleState");
  private static readonly int kAnimEmoteState = Animator.StringToHash("EmoteState");
  private static readonly int kAnimEmoteName = Animator.StringToHash("Emote");

  public void PlayEmote(AstronautEmote emote)
  {
    _animator.SetFloat(kAnimEmoteState, (float)emote);
    _animator.Play(kAnimEmoteName, 0, 0);
  }

  public void PressInteraction()
  {
    if (_attackCooldownTimer <= 0 && !_roomInhabitant.IsBeingSuckedIntoSpace && !IsStunned)
    {
      _attackCooldownTimer = _attackCooldown;
      PlayEmote(AstronautEmote.Attack);

      GameObject attackFx = Instantiate(_attackFxPrefab, transform.position, transform.rotation);
      Destroy(attackFx, 3.0f);

      if (_roomInhabitant.CurrentDevice != null)
      {
        _roomInhabitant.CurrentDevice.TriggerInteraction(gameObject);

        // Always drain the battery after all interactions (if not drained already)
        if (_batteryComponent != null && _roomInhabitant.CurrentDevice.DrainsBatteryOnInteraction())
        {
          _batteryComponent.DrainCharge();
        }
      }

      if (_canStun)
      {
        TryWhackAstronaut();
      }
    }
  }

  private void Awake()
  {
    if (_exclamationEffect != null)
    {
      _exclamationEffect.SetActive(false);
    }
  }

  private void Start()
  {
    GameObject spawnfx = Instantiate(_spawnFxPrefab, transform.position, transform.rotation);
    Destroy(spawnfx, 10.0f);

    GameObject headPrefab = _headPrefabs[Random.Range(0, _headPrefabs.Length)];
    _headObj = Instantiate(headPrefab, _headSpawnRoot);
    _headObj.transform.localPosition = Vector3.zero;
    _headObj.transform.localRotation = Quaternion.identity;
  }

  private void OnEnable()
  {
    _instances.Add(this);
  }

  private void OnDisable()
  {
    _instances.Remove(this);
  }

  private void Update()
  {
    // Orient to face movement direction
    if (_rb.velocity.sqrMagnitude > 0.01f && !IsStunned)
    {
      Quaternion desiredRot = Quaternion.LookRotation(_rb.velocity, Vector3.up);
      transform.rotation = Mathfx.Damp(transform.rotation, desiredRot, 0.25f, Time.deltaTime * 5);
    }

    // Roll based on movement
    float targetZRot = Mathf.Abs(_moveVector.x) > 0.1f ? Mathf.Sign(_moveVector.x) * -90 : 0;
    if (IsStunned || _roomInhabitant.IsBeingSuckedIntoSpace)
    {
      targetZRot = 0;
    }

    _zRot = Mathfx.Damp(_zRot, targetZRot, 0.5f, Time.deltaTime * 5);
    _visualRoot.localEulerAngles = _visualRoot.localEulerAngles.WithZ(_zRot);

    // Update idle anim state 
    if (_roomInhabitant.IsBeingSuckedIntoSpace)
    {
      _currentIdleState = AstronautIdle.Panic;
    }
    else if (IsStunned)
    {
      _currentIdleState = AstronautIdle.Stunned;
    }
    else if (_moveVector.sqrMagnitude > 0.01f)
    {
      _currentIdleState = AstronautIdle.Move;
    }
    else
    {
      _currentIdleState = AstronautIdle.Idle;
    }

    // Blend idle state
    _idleBlend = Mathfx.Damp(_idleBlend, (float)_currentIdleState, 0.25f, Time.deltaTime * 5);
    _animator.SetFloat(kAnimIdleState, _idleBlend);

    // Handle sucked into space 
    if (_roomInhabitant.IsBeingSuckedIntoSpace)
    {
      if (_hideOnDie[0].activeSelf)
      {
        foreach (GameObject obj in _hideOnDie)
          obj.SetActive(false);

        foreach (GameObject obj in _showOnDie)
          obj.SetActive(true);
      }

      _deathTimer += Time.deltaTime;
      if (!_isDead && (_roomInhabitant.Room == null || _deathTimer > 5))
      {
        _rb.AddForce(Vector3.up * 5, ForceMode.VelocityChange);
        _rb.AddTorque(Random.onUnitSphere * 1, ForceMode.VelocityChange);
        _isDead = true;

        if (_deathSound)
          AudioManager.Instance.PlaySound(gameObject, _deathSound);

        Died?.Invoke();
      }

      if (_isDead && (!Mathfx.IsPointInViewport(transform.position, Camera.main) || _deathTimer > 10))
      {
        StartCoroutine(DieAsync());
        return;
      }
    }

    if (_stunFx != null)
    {
      _stunFx.SetActive(IsStunned);
    }

    if (!_roomInhabitant.IsBeingSuckedIntoSpace)
      _attackCooldownTimer -= Time.deltaTime;

    if (_exclamationEffect != null)
    {
      _exclamationEffect.SetActive(_attackCooldownTimer > 0);
    }

    _stunTimer -= Time.deltaTime;
  }

  private void OnCollisionEnter(Collision col)
  {
    _isColliding = true;
  }

  private void OnCollisionExit(Collision col)
  {
    _isColliding = false;
  }

  private void FixedUpdate()
  {
    // Move out of spaceship when sucked out
    if (_roomInhabitant.IsBeingSuckedIntoSpace)
    {
      if (_isColliding)
      {
        _rb.constraints = RigidbodyConstraints.None;
        _rb.AddForce(Vector3.up * Time.deltaTime * 10, ForceMode.Acceleration);
      }
    }
    // Apply movement to physics
    else
    {
      if (!IsStunned)
      {
        _rb.AddForce(MoveVector * _acceleration * Time.deltaTime, ForceMode.Acceleration);
      }

      _rb.velocity = Vector3.ClampMagnitude(_rb.velocity, _maxSpeed);
    }
  }

  private IEnumerator DieAsync()
  {
    Vector3 startScale = transform.localScale;
    const float duration = 1;
    for (float time = 0; time < 1; time += Time.deltaTime)
    {
      float t = time / duration;
      transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
      yield return null;
    }

    Destroy(gameObject);
    Despawned?.Invoke();
    Destroy(gameObject);
  }

  private void TryWhackAstronaut()
  {
    for (int i = 0; i < AstronautController.Instances.Count; ++i)
    {
      AstronautController astro = AstronautController.Instances[i];
      if (astro != this)
      {
        Vector3 toAstro = (astro.transform.position - transform.position).WithY(0);
        if (toAstro.magnitude < 2.5f)
        {
          if (_hitSound)
            AudioManager.Instance.PlaySound(gameObject, _hitSound);

          astro.GetWhacked(transform.position);

          BatteryComponent battery = GetComponent<BatteryComponent>();
          if (battery != null && battery.HasCharge)
          {
            astro.RoomInhabitant.NotifySuckedIntoSpace();
            battery.DrainCharge();
          }

          return;
        }
      }
    }
  }

  private void GetWhacked(Vector3 fromPos)
  {
    Debug.Log($"{name} got whacked");
    _rb.AddForce((transform.position - fromPos).normalized * 10, ForceMode.VelocityChange);
    PlayEmote(AstronautEmote.HitReact);
    _stunTimer = 5;
  }
}