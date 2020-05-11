using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;


/// <summary>
/// Player component. Manages inputs, character states and associated game flow.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour {

    public static Player Instance = null;

    [System.Serializable]
    public class MovementParameters
    {
        public float speedMax = 2.0f;
        public float acceleration = 12.0f;
        public float friction = 12.0f;
    }


    // Possible orientation for player aiming : 4 direction, 8 direction or free direction (for analogic joysticks)
    public enum ORIENTATION
    {
        FREE,
        DPAD_8,
        DPAD_4
    }

    // Character can only be at one state at a time. For example, he can't attack and be stunned at the same time.
    public enum STATE
    {
        IDLE = 0,
        ATTACKING = 1,
        STUNNED = 2,
        DEAD = 3,
    }


    // Life and hit related attributes
    [Header("Life")]
    public int life = 3;
    public float invincibilityDuration = 1.0f;
    public float invincibilityBlinkPeriod = 0.2f;
    public LayerMask hitLayers;
    public float knockbackSpeed = 3.0f;
    public float knockbackDuration = 0.5f;

    private float _lastHitTime = float.MinValue;
    private List<SpriteRenderer> _spriteRenderers = new List<SpriteRenderer>();
    private Coroutine _blinkCoroutine = null;


    // Movement attributes
    [Header("Movement")]
    public MovementParameters defaultMovement = new MovementParameters();
    public MovementParameters stunnedMovement = new MovementParameters();

    private Rigidbody2D _body = null;
    private Vector2 _direction = Vector2.zero;
    private MovementParameters _currentMovement = null;

    // Attack attributes
    [Header("Attack")]
    public GameObject attackPrefab = null;
    public GameObject attackSpawnPoint = null;
    public float attackCooldown = 0.3f;
    public ORIENTATION orientation = ORIENTATION.FREE;

    private float lastAttackTime = float.MinValue;


    // Input attributes
    [Header("Input")]
    [Range(0.0f, 1.0f)]
    public float controllerDeadZone = 0.3f;

    // State attributes
    private STATE _state = STATE.IDLE;

    // Collectible attributes
    private int _keyCount;
    public int KeyCount { get { return _keyCount; } set { _keyCount = value; } }

	// Dungeon position
	private Room _room = null;
	public Room Room { get { return _room; } }


	// Use this for initialization
	private void Awake () {
        Instance = this;
        _body = GetComponent<Rigidbody2D>();
        GetComponentsInChildren<SpriteRenderer>(true, _spriteRenderers);
    }

    private void Start()
    {
        SetState(STATE.IDLE);
    }

    // Update is called once per frame
    private void Update () {
        UpdateState();
        UpdateInputs();
	}

    // Update physics on FixedUpdate (FixedUpdate can be called multiple times a frame).
    private void FixedUpdate()
    {
        FixedUpdateMovement();
		FixedUpdateRoom();
	}

	private void FixedUpdateRoom()
	{
		Collider2D[] colliders = Physics2D.OverlapPointAll(transform.position);
		if(colliders != null && colliders.Length > 0)
		{
			foreach(Collider2D collider in colliders)
			{
				if(collider.gameObject.tag == "Door")
				{
					Room room = collider.gameObject.GetComponentInParent<Room>();
					if(room && room != _room)
					{
						room.OnEnterRoom();
					}
				}
			}
		}
	}

	// Update inputs
	private void UpdateInputs()
    {
        if (CanMove())
        {
            _direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (_direction.magnitude < controllerDeadZone)
            {
                _direction = Vector2.zero;
            } else {
                _direction.Normalize();
            }
            if(Input.GetButtonDown("Fire1")) {
                Attack();
            }
        } else {
            _direction = Vector2.zero;
        }
    }

    // Update states
    private void UpdateState()
    {
        switch(_state)
        {
			case STATE.ATTACKING:
				SpawnAttackPrefab();
				SetState(STATE.IDLE);
				break;
			default: break;
        }
    }

    // Set state exits previous state, change state then enter new state. Instructions related to exiting and entering a state are in the two "switch(_state){...}" of this method.
    private void SetState(STATE state)
    {
        // Exit previous state
        // switch (_state)
        //{
        //}

        _state = state;
        // Enter new state
        switch (_state)
        {
            case STATE.STUNNED: _currentMovement = stunnedMovement; break;
            case STATE.DEAD: EndBlink(); break;
            default: _currentMovement = defaultMovement; break;
        }

        // Reset direction if player cannot move in this state
        if (!CanMove())
        {
            _direction = Vector2.zero;
        }
    }


    // Update movement and friction
    void FixedUpdateMovement()
    {
        if (_direction.magnitude > Mathf.Epsilon) // magnitude > 0
        {
            // If direction magnitude > 0, Accelerate in direction, then clamp velocity to max speed. Do not apply friction if character is moving toward a direction.
            _body.velocity += _direction * _currentMovement.acceleration * Time.fixedDeltaTime;
            _body.velocity = Vector2.ClampMagnitude(_body.velocity, _currentMovement.speedMax);
            transform.eulerAngles = new Vector3(0.0f, 0.0f, ComputeOrientationAngle(_direction));
        } else {
            // If direction magnitude == 0, Apply friction
            float frictionMagnitude = _currentMovement.friction * Time.fixedDeltaTime;
            if (_body.velocity.magnitude > frictionMagnitude)
            {
                _body.velocity -= _body.velocity.normalized * frictionMagnitude;
            } else {
                _body.velocity = Vector2.zero;
            }
        }
    }

	// Attack method sets player in attack state. Attack prefab is spawned by calling SpawnAttackPrefab method.
	private void Attack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
            return;
        lastAttackTime = Time.time;
        SetState(STATE.ATTACKING);
    }

    // This method spawns the associated prefab on attackSpawnPoint.
    private void SpawnAttackPrefab()
    {
        if (attackPrefab == null)
            return;

        // transform used for spawn is attackSpawnPoint.transform if attackSpawnPoint is not null. Else it's transform.
        Transform spawnTransform = attackSpawnPoint ? attackSpawnPoint.transform : transform;
        GameObject.Instantiate(attackPrefab, spawnTransform.position, spawnTransform.rotation);
    }

    // Applyhit is called when player touches an enemy hitbox or any hazard.
    public void ApplyHit(Attack attack)
    {
        if (Time.time - _lastHitTime < invincibilityDuration)
            return;
        _lastHitTime = Time.time;

        life -= (attack != null ? attack.damages : 1);
        if (life <= 0)
        {
            SetState(STATE.DEAD);
        } else {
            if (attack != null && attack.knockbackDuration > 0.0f)
            {
                StartCoroutine(ApplyKnockBackCoroutine(attack.knockbackDuration, attack.transform.right * attack.knockbackSpeed));
            }
            EndBlink();
            _blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }
    }

    // ApplyKnockBackCoroutine puts player in STUNNED state and sets a velocity to knockback player. It resume to IDLE state after a fixed duration. STUNNED state has his own movement parameters that allow to redefine frictions when character is knocked.
    private IEnumerator ApplyKnockBackCoroutine(float duration, Vector3 velocity)
    {
        SetState(STATE.STUNNED);
        _body.velocity = velocity;
        yield return new WaitForSeconds(duration);
        SetState(STATE.IDLE);
    }

    // BlinkCoroutine makes all sprite renderers in the player hierarchy blink from enabled to disabled with a fixed period over a fixed time.  
    private IEnumerator BlinkCoroutine()
    {
        float invincibilityTimer = 0;
        while(invincibilityTimer < invincibilityDuration)
        {
            invincibilityTimer += Time.deltaTime;
            bool isVisible = ((int)(invincibilityTimer / invincibilityBlinkPeriod)) % 2 == 1;
            foreach(SpriteRenderer spriteRenderer in _spriteRenderers)
            {
                spriteRenderer.enabled = isVisible;
            }
            yield return null; // wait next frame
        }
        EndBlink();
    }

    // Stops current blink coroutine if any is started and set all sprite renderers to enabled.
    private void EndBlink()
    {
        if (_blinkCoroutine == null)
            return;
        foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
        {
            spriteRenderer.enabled = true;
        }
        StopCoroutine(_blinkCoroutine);
        _blinkCoroutine = null;

    }

    // Transforms the orientation vector into a discrete angle.
    private float ComputeOrientationAngle(Vector2 direction)
    {
        float angle = Vector2.SignedAngle(Vector2.right, direction);
        switch(orientation)
        {
            case ORIENTATION.DPAD_8: return Utils.DiscreteAngle(angle, 45); // Only 0 45 90 135 180 225 270 315
            case ORIENTATION.DPAD_4: return Utils.DiscreteAngle(angle, 90); // Only 0 90 180 270
            default: return angle;
        }
    }

    // Can player moves or attack
    private bool CanMove()
    {
        return _state == STATE.IDLE;
    }

	public void EnterRoom(Room room)
	{
		_room = room;
	}

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if( ((1 << collision.gameObject.layer) & hitLayers) != 0 )
        {
            // Collided with hitbox
            Attack attack = collision.gameObject.GetComponent<Attack>();
            ApplyHit(attack);
        }
    }
}
