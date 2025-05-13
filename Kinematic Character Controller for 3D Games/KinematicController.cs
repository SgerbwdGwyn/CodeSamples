//#define USE_BOUNCING

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using Twyn.Physics;
using TwynInput;
using Utils;
using Twyn.Timing;

namespace Twyn.Physics {
	public enum EFakeParentType {
		Ground,
		Volume
	}
}

public class KinematicController : MonoBehaviour {

	#region Drag Drop
	// Collision resolving is done with respect to this volume

	[SerializeField]
	protected CapsuleCollider _collisionVolume;

	//[SerializeField]
	public Transform _camTransform; //"eyes"
	Camera m_camera;

	// Collision will not happen with these layers
	// One of them has to be this controller's own layer
	[SerializeField]
	protected LayerMask _excludedLayers;

	[SerializeField]
	protected List<Transform> _groundedRayPositions;

	[SerializeField]
	protected double _slopePenetrationIgnoreLimit = 0.015;

	[SerializeField]
	protected float maxSlopeAngle = 45f;

	[SerializeField]
	protected float minimumVelocityBounce = 20.0f;

	[SerializeField]
	protected float restitutionFactor = 0.5f;

	//[SerializeField]
	public float SENSITIVITY = 100f;

	#endregion

	#region Movement Parameters

	// The controller can collide with colliders within this radius
	protected const float Radius = 2f;

	// Ad-hoc approach to make the controller accelerate faster
	//protected const float GroundAccelerationCoeff = 500.0f;
	//protected const float GroundAccelerationCoeff = 150.0f; // TF2 version
	// WAS 150, now 250
	[SerializeField]
	protected float GroundAccelerationCoeff = 250.0f; // TF2 version

	// How fast the controller accelerates while it's not grounded
	[SerializeField]
	protected float AirAccelCoeff = 3f; // TF2 Version
	[SerializeField]
	protected float AirDecelCoeff = 8f; // TF2 Version
	protected float BoardingGroundAccelMultiplier = 0.1f; // If boarding, multiplies onto the ground accel value

	// Along a dimension, we can't go faster than this
	// This dimension is relative to the controller, not global
	// Meaning that "max speend along X" means "max speed along 'right side' of the controller"
	//protected float MaxSpeedAlongOneDimension = 8f;
	//protected const float MaxSpeedDefault = 8f;
	public float MaxSpeedDefault = 10f; // Upped to 10 from 8
	public float MaxSpeedSideways = 8f;
	public float MaxSpeedBackwards = 4f;
	public const float SpeedLimitSoftAir = 1.3f; // in multiples of the base/walking speed limit - applies to player input
	public const float SpeedLimitSoftBoarding = 2.0f; // in multiples of the base/walking speed limit - applies to player input while boarding
	public const float SpeedLimitHard = 8.0f; // in multiples of the base/walking speed limit - applies to any source of speed, incl. explosions

	public float SpeedLimitSoft { get { return m_boardEnabled ? SpeedLimitSoftBoarding : SpeedLimitSoftAir; }	}

	// How fast the controller decelerates on the grounded
	// Was previously 9, decreased to 3f
	//protected const float Friction = 4.5f; // TF2 version
	[SerializeField]
	protected float Friction = 3f; // TF2 version
								   //protected const float SlidingFriction = 0.05f; // TF2 version

	// Stop if under this speed
	protected const float FrictionSpeedThresholdStop = 0.1f;

	protected const float IncreaseFrictionSpeedThresholdUpper = 12f;	// Use extra friction multiplier if under this speed
	protected const float IncreaseFrictionSpeedThresholdLower = 2.0f;	// Hits maximum increase here

	protected const float IncreaseFrictionFactor = 8f;
	protected const float IncreaseFrictionFactorBoarding = 44f;

	protected const float FrictionMultiplierBoarding = 0.03f;

	// Push force given when jumping
	// - Used also to determine whether or not to ground us when sliding across a surface.
	//protected const float JumpStrength = 8f;
	[SerializeField]
	protected float JumpStrength = 10f;

	// yeah...
	[SerializeField]
	public float GravityAmount = 24f;
	public const float GravityMultBoardingUphill = 0.5f;	// Applies only when grounded
	public const float GravityMultBoardingDownhill = 1.5f;	// Applies only when grounded

	// How precise the controller can change direction while not grounded 
	protected const float AirControlPrecision = 8f; // 16

	// Should we increase air control/direction change when moving forward only?
	//protected const bool bForwardAirControlAddition = false; // true

	// When moving only forward, increase air control dramatically
	protected const float AirControlAdditionForward = 2f; //8

	// Prevents the projection of velocity onto the ground normal plane that stops us bouncing down slopes etc. Used to allow the player to be launched vertically when grounded.
	protected bool beingLaunched = false;

	protected bool isRamping = false; // Causes sliding state when going up ramps
									  //protected float rampThreshold = JumpStrength + 0.05f;
	protected float rampThreshold;

	// Sandboarding:
	protected bool m_boardEnabled = false;

	#endregion

	#region Fields
	// The real velocity of this controller
	protected Vector3 _velocity;        // Local velocity
	protected Vector3 _velocity_ws;     // World-space movement per tick

	public Vector3 GetVelocity() { return _velocity; }
	public void SetVelocity(Vector3 vel) { _velocity = vel; }

	protected float verticalVelocityBeforeLanding;

	[HideInInspector]
	public bool _isGrounded = false;

	protected Collider m_pCollidersThis;

	// Raw input taken with GetAxisRaw()
	//protected Vector3 _moveInput;

	// Vertical look
	[HideInInspector]
	public float _pitch = 0; // We keep track of this stat since we want to clamp it
	public Vector2 pitchLimit = new Vector2(-360, 360);

	[HideInInspector]
	public float _yaw = 0;

	protected readonly Collider[] _overlappingColliders = new Collider[30]; // More may be needed in future?
	protected Transform _ghostJumpRayPosition; // Removed for now - ruins downhill sliding
											   // Old stat was (0, -0.5, -0.75)

	// Some information to persist
	protected bool _isGroundedInPrevFrame;
	protected bool _isGonnaJump;
	protected Vector3 _wishDirDebug;

	protected const float _rampDoubleJumpTimeMax = 0.3f; // If a 2nd jump occurs within X seconds, convert some horizontal to vertical speed
	protected float _timeSinceLastJump = 0.0f;
	protected const float kJumpMinimumInterval = 0.1f;

	// Was the last jump a ramp/double jump? If so, don't supply the down-jump boost.
	protected bool _lastjumpwasramp = false;

	protected Vector3[] velocityReadings;
	protected const int NUMREADINGS = 60;

	// Grounding Constraints:
	protected Transform m_groundingEntity;
	public Transform m_fakeParentEntity { get; protected set; }
	protected EFakeParentType m_fakeParentType = EFakeParentType.Ground;

	protected Vector3 m_fakeParentPositionPrevious;
	protected Quaternion m_fakeParentRotationPrevious;
	protected bool m_groundingVelocityClipPending; // If true, we are to delay applying the fake parent transform for 1 frame so we can calculate the effective velocity in ws at the position
	protected bool m_fakeParentAdded = false; // Set to true when we initially become FP'd to a volume, so we can clip velocity

	protected Vector3 m_previousPosition_ws; // Player previous position (as of last fixedupate) used to calculate world space velocity when unseating

	Vector3 dbg_motionFromFakeParent;

	public void SetFakeParent(Transform fakeParentEntity, EFakeParentType type) {
		if (m_fakeParentEntity != null && m_fakeParentEntity == fakeParentEntity) {
			// No need to set if it's already set to this
			Debug.Log(Time.time.ToString("F2") + ": Tried to set FP to " + (fakeParentEntity == null ? "null" : fakeParentEntity.name) + ", cancelled due to FP already being that.");
			return;
		}

		if (m_fakeParentEntity != null && m_fakeParentType == EFakeParentType.Volume && type == EFakeParentType.Ground) {
			// Don't overwrite a volume with a ground type
			Debug.Log(Time.time.ToString("F2") + ": Tried to set FP to " + (fakeParentEntity == null ? "null" : fakeParentEntity.name) + ", cancelled due to volume existing.");
			return;
		}

		// If we're unsetting this, we should apply motion as velocity, so we inherit the motion
		if (fakeParentEntity == null) {
			// If we're removing a volume type parent, check to see if there is a grounding entity we can switch to
			if (type == EFakeParentType.Volume) {
				if (m_groundingEntity != null) {
					fakeParentEntity = m_groundingEntity;
					type = EFakeParentType.Ground;
				}
			}

			// If there's no grounding entity to fall back on, then just apply the velocity
			if (fakeParentEntity == null) {
				ApplyFakeParentMotionAsVelocity();
			}
		}

		if (fakeParentEntity != null) m_fakeParentAdded = true;

		m_fakeParentEntity = fakeParentEntity;
		m_fakeParentType = type;
		//Debug.Log(Time.time.ToString("F2") + ": Set FP to " + (fakeParentEntity == null ? "null" : fakeParentEntity.name));

		if (m_fakeParentEntity != null) {
			m_fakeParentPositionPrevious = m_fakeParentEntity.position;
			m_fakeParentRotationPrevious = m_fakeParentEntity.rotation;
		} else {
			m_fakeParentPositionPrevious = Vector3.zero;
			m_fakeParentRotationPrevious = Quaternion.identity;
		}
	}

	public bool m_hasFakeParent {
		get {
			return m_fakeParentEntity != null;
		}
	}


	//protected float verticalVelocityBeforeLanding = 0f; // What was our vertical velocity last frame?

	[HideInInspector]
	public bool pitchShouldAffectModel {
		get {
			return _pitchShouldAffectModel;
		}
		set {
			if (value != _pitchShouldAffectModel) {
				_pitchShouldAffectModel = value;
				ResetPitchRotation();
				//Debug.Log("Reset pitch rot");
			}
		}
	}

	void ResetPitchRotation() {
		transformForPitch.localRotation = defaultPitchObjectRot;
	}

	private bool _pitchShouldAffectModel = false;
	[SerializeField]
	protected Transform transformForPitch = null;
	protected Quaternion defaultPitchObjectRot;

	#endregion

	#region InputsVariables
	public Vector2 m_Look;
	public Vector2 m_Move;
	#endregion

	//protected Vector3 _averageVelocity;
	public Vector3 averageVelocity {
		get {
			Vector3 avg = Vector3.zero;
			for (int i = 0; i < NUMREADINGS; i++) {
				avg += velocityReadings[i];
			}
			avg /= NUMREADINGS;
			return avg;
		}
	}

	public enum EControllerState {
		Normal,
		Seated
	}
	public EControllerState m_currentState { get; protected set; } = EControllerState.Normal;

	protected void Awake() {
		m_camera = _camTransform.GetComponent<Camera>();
	}

	protected void Start() {
		_ghostJumpRayPosition = _groundedRayPositions.Last();

		rampThreshold = JumpStrength * 0.8f;

		velocityReadings = new Vector3[NUMREADINGS];
		for (int i = 0; i < NUMREADINGS; i++) {
			velocityReadings[i] = Vector3.zero;
		}

		if (transformForPitch)
			defaultPitchObjectRot = transformForPitch.localRotation; // Cache this.

		Collider[] arrColliders = GetComponents<Collider>();
		foreach (Collider pCollider in arrColliders) {
			if (!pCollider.isTrigger) {
				m_pCollidersThis = pCollider;
				break;
			}
		}

		Cursor.lockState = CursorLockMode.Locked;

		_velocity = _velocity_ws = Vector3.zero;
	}

	public void Update() {
		_pitch += m_Look.y * -SENSITIVITY;
		//_yaw += m_Look.x * SENSITIVITY;
		float yawDeltaThisFrame = m_Look.x * SENSITIVITY;

		// Wrap the angle
		_pitch.Wrap180();
		//_yaw.Wrap180();

		_pitch = Mathf.Clamp(_pitch, pitchLimit.x, pitchLimit.y);

		// 200 IS THE SENS, MOVE IT TO CONFIG 
		//_pitch += (m_Look.y * -SENSITIVITY);

		// Pitch:
		_camTransform.localRotation = Quaternion.Euler(Vector3.right * _pitch);

		// Enable for pitch affecting model:
		/*if (pitchShouldAffectModel && transformForPitch) {
			transformForPitch.localRotation = defaultPitchObjectRot * Quaternion.Euler(Vector3.right * _pitch);
			Debug.DrawLine(transformForPitch.position, transformForPitch.position + transformForPitch.forward * 30.0f, Color.white);
		}*/

		// Yaw:
		//Vector3 worldRot = transform.rotation.eulerAngles;
		//worldRot.x = worldRot.z = 0.0f;
		//transform.rotation = Quaternion.Euler(worldRot);
		//transform.localRotation = Quaternion.Euler(Vector3.up * _yaw);
		transform.Rotate(Vector3.up * yawDeltaThisFrame);

		//_camTransform.Rotate(_camTransform.forward, -_currentRot, Space.World);
	}


	protected int velRecCounter = 0;
	protected virtual void FixedUpdate() {
		if (m_currentState == EControllerState.Seated) {
			//...
		} else {
			// Grab the vertical velocity to calculate the impact speed if necessary
			if (!_isGrounded)
				verticalVelocityBeforeLanding = _velocity.y < 0 ? _velocity.y : verticalVelocityBeforeLanding;

			//Average speed readings
			//velocityReadings[velRecCounter] = _velocity.ToHorizontal();
			//if (++velRecCounter > (NUMREADINGS - 1)) {
			//	velRecCounter = 0;
			//}

			var dt = Time.fixedDeltaTime;

			//if (_timeSinceLastJump < kJumpMinimumInterval + 0.1f) {
			//	_timeSinceLastJump += dt;
			//	//Debug.Log(Time.time + " time since last jump " + _timeSinceLastJump);
			//}

			// The player is attempting to go in this direction
			Vector3 wishDir = new Vector3(m_Move.x, 0f, m_Move.y);
			wishDir = _camTransform.TransformDirectionHorizontal(wishDir);
			_wishDirDebug = wishDir;//.ToHorizontal();

			// Apply fake parent transform before grounding check in case the parent has moved enough this past frame to incorrectly unground us:
			if (m_hasFakeParent && m_currentState != EControllerState.Seated) {
				transform.position = ApplyFakeParentTransform(transform.position);
			}

			Vector3 groundNormal;
			_isGrounded = IsGrounded(out groundNormal);
			_isGrounded = beingLaunched ? false : _isGrounded;

			// Need to do this if we've become FP'd to something this/last frame
			CheckGroundingParentConstraint();
			bool clipVelocityNextFrame = false;
			if (m_fakeParentAdded) {
				clipVelocityNextFrame = true;
				m_fakeParentAdded = false;
			}

			bool bounceForNext = false;

			if (_isGrounded) {
				if (_isGroundedInPrevFrame) {
					// Don't apply friction if just landed or about to jump (essentially, bhopping), or if we're sandboarding
					if (!_isGonnaJump && !beingLaunched && !m_groundingVelocityClipPending) { //TEMP TEMP TEMP
						ApplyFriction(ref _velocity, dt, wishDir);
					}
				} else {
#if USE_BOUNCING
					if (-verticalVelocityBeforeLanding > minimumVelocityBounce) {
						bounceForNext = true;
					}
#endif
				}

				if (!_isGonnaJump) {
					Accelerate(ref _velocity, wishDir, GroundAccelerationCoeff * (m_boardEnabled ? BoardingGroundAccelMultiplier : 1.0f), dt);
				}

				// Crop up horizontal velocity component - seems we don't want to do this at frame +0 or +1 from being grounded?
				//if (!beingLaunched && !clipVelocityNextFrame && !m_groundingVelocityClipPending) {
				//	_velocity = Vector3.ProjectOnPlane(_velocity, groundNormal);
				//}

				if (!_isGroundedInPrevFrame && !_isGonnaJump) {
					_lastjumpwasramp = false;
				}

				// JUMP
				if (_isGonnaJump && !isRamping) {
					//if (_timeSinceLastJump > kJumpMinimumInterval)
					//{
					_velocity += groundNormal * JumpStrength; // New: GroundNormal only

					// The minimum time needed between jumps to reset them (Too frequently will not allow the timer to be reset)
					//_timeSinceLastJump = 0.0f;

					//Debug.Log(Time.time + " JUMP");
					//}
				}

				if (m_boardEnabled) {
					bool downhill = Vector3.Dot(_velocity.normalized, Util.Gravity) > 0;
					float boardingGravity = GravityAmount * (downhill ? GravityMultBoardingDownhill : GravityMultBoardingUphill);

					_velocity += Util.Gravity * (GravityAmount * dt);
				}

			} else {
				// If the input doesn't have the same facing with the current velocity
				// then slow down instead of speeding up (and use appropriate coefficient)
				var coeff = Vector3.Dot(_velocity, wishDir) > 0 ? AirAccelCoeff : AirDecelCoeff;

				Accelerate(ref _velocity, wishDir, coeff, dt);

				if (Mathf.Abs(m_Move.x) > 0.0001) {  // Pure side velocity doesn't allow air control
					ApplyAirControl(ref _velocity, wishDir, dt);
				}

				_velocity += Util.Gravity * (GravityAmount * dt);
			}

			// Hard limit check:
			if (_velocity.magnitude > (MaxSpeedDefault * SpeedLimitHard)) {
				_velocity *= _velocity.magnitude / (MaxSpeedDefault * SpeedLimitHard);
			}

			Vector3 displacement = _velocity * dt;

			// If we're moving too fast, make sure we don't hollow through any collider
			if (displacement.magnitude > _collisionVolume.radius) {
				ClampDisplacement(ref _velocity, ref displacement, transform.position);
			}

			transform.position += displacement;

			Vector3 collisionDisplacement = ResolveCollisions(ref _velocity, false);
			transform.position += collisionDisplacement;

			// We're +1 frame from when we were initially grounded, so we can work out the effective velocity due to the grounding entity's motion
			if (m_groundingVelocityClipPending) {
				ClipGroundingVelocity();
				m_groundingVelocityClipPending = false;
			}

			// We *would* avoid doing this for the first frame we become grounded, but the parent transformation delta will never be non-zero at this point so it's fine.
			// This is so we can calculate the velocity of the player provided by the platform's motion.

			// REMOVED: We want to do this before checking grounding so that a downward moving platform doesn't unground us.
			// We would need to do this here if the parent previous pos/rot were different to the current at this point, but they never will be if we've become grounded this frame
			//if (!_isGroundedInPrevFrame) {
			//	transform.position = ApplyFakeParentTransform(transform.position);
			//}

			// If we have become grounded this frame, we need to measure our effective WS velocity and clip our velocity based on that
			if (clipVelocityNextFrame) {
				m_groundingVelocityClipPending = true;
			}

			if (beingLaunched) {
				beingLaunched = false;
			}

#if USE_BOUNCING
			if (bounceForNext) {
				AddForce(new Vector3(0, -verticalVelocityBeforeLanding * restitutionFactor, 0), true);
				//_isGrounded = false;
				Debug.Log("Bounced up: " + _velocity.y.ToString());
			}
#endif
		}

		// Calculate world-space velocity:
		_velocity_ws = (transform.position - m_previousPosition_ws) / Time.fixedDeltaTime;
		m_previousPosition_ws = transform.position;

		_isGroundedInPrevFrame = _isGrounded;
		//m_positionPreviousFrame = transform.position;

		if (m_hasFakeParent) {
			m_fakeParentRotationPrevious = m_fakeParentEntity.rotation;
			m_fakeParentPositionPrevious = m_fakeParentEntity.position;
		}

		if (!_isGrounded) {
			m_groundingEntity = null;
		}
	}

	protected void Accelerate(ref Vector3 playerVelocity, Vector3 accelDir, float accelCoeff, float dt) {
		// How much speed we already have in the direction we want to speed up

		// CPMA Style - Acceleration in direction of vel = 0, at 90° = 1, at 180° (Braking) = 2
		float projSpeed = Vector3.Dot(playerVelocity, accelDir);
		float horizontalSpeedBeforeAcceleration = playerVelocity.horizontalMagnitude();

		// Beware floating point imprecision!
		float speedToUse = MaxSpeedDefault;
		if (m_Move.y < 0) {
			speedToUse = MaxSpeedBackwards;
		} else if (Mathf.Abs(m_Move.x) > 0.05f) {
			speedToUse = MaxSpeedSideways;
		}

		// How much speed we need to add (in that direction) to reach max speed
		var addSpeed = speedToUse - projSpeed;
		if (addSpeed <= 0) {
			return;
		}

		// How much we are gonna increase our speed
		// maxSpeed * dt => the real deal. a = v / t
		// accelCoeff => ad hoc approach to make it feel better
		var accelAmount = accelCoeff * speedToUse * dt;

		// If we are accelerating more than in a way that we exceed maxSpeedInOneDimension, crop it to max
		if (accelAmount > addSpeed) {
			accelAmount = addSpeed;
		}

		playerVelocity += accelDir * accelAmount;



		// To prevent hyper velocity with airstrafing:
		float horizontalSpeedAfterAcceleration = playerVelocity.horizontalMagnitude();

		// If our new velocity is above the speed limit... (or was already)
		if (horizontalSpeedAfterAcceleration > MaxSpeedDefault * SpeedLimitSoft) {
			// ...Then set our speed to whatever it was before acceleration.
			// This allows us to change direction without increasing speed, but retaining speed from explosions etc.
			playerVelocity.x *= (horizontalSpeedBeforeAcceleration / horizontalSpeedAfterAcceleration);
			playerVelocity.z *= (horizontalSpeedBeforeAcceleration / horizontalSpeedAfterAcceleration);
		}
	}

	protected void ApplyFriction(ref Vector3 playerVelocity, float dt, Vector3 wishdir) {
		var speed = playerVelocity.magnitude;
		if (speed <= 0.00001) {
			return;
		}

		// I think this is here to give us greater control??
		float dot = (wishdir.magnitude > 0.1f) ? Mathf.Max(0f, Vector3.Dot(wishdir, playerVelocity)) : 1f;

		var downLimit = Mathf.Max(speed, FrictionSpeedThresholdStop); // Don't drop below threshold.
																	  // If we're crouching and going beyond the minimum slide limit and we allow sliding, use sliding friction

		float fric = m_boardEnabled ? FrictionMultiplierBoarding : Friction;
		if (speed < IncreaseFrictionSpeedThresholdUpper) {
			float frictionModifier = speed.RemapClamped(IncreaseFrictionSpeedThresholdLower, IncreaseFrictionSpeedThresholdUpper, m_boardEnabled ? IncreaseFrictionFactorBoarding : IncreaseFrictionFactor, 1.0f);
			fric *= frictionModifier;
		}

		// Don't apply the dot if we're boarding
		float dropAmount = Mathf.Max(speed - (downLimit * fric * dt * (m_boardEnabled ? 1.0f : dot)), 0.0f);

		playerVelocity *= (dropAmount / speed); // Reduce the velocity by a certain percent
	}

	protected void ApplyAirControl(ref Vector3 playerVelocity, Vector3 accelDir, float dt) {
		// This only happens in the horizontal plane
		// TODO: Verify that these work with various gravity values
		var playerDirHorz = playerVelocity.ToHorizontal().normalized;
		var playerSpeedHorz = playerVelocity.ToHorizontal().magnitude;

		var dot = Vector3.Dot(playerDirHorz, accelDir);
		if (dot > 0) {
			var k = AirControlPrecision * dot * dot * dt;

			// Like CPMA, increased direction change rate when we only hold W
			// If we want pure forward movement, we have much more air control
			var isPureForward = Mathf.Abs(m_Move.x) < 0.0001 && Mathf.Abs(m_Move.y) > 0;
			if (isPureForward) {
				k *= AirControlAdditionForward;
			}

			// A little bit closer to accelDir
			playerDirHorz = playerDirHorz * playerSpeedHorz + accelDir * k;
			playerDirHorz.Normalize();

			// Assign new direction, without touching the vertical speed
			playerVelocity = (playerDirHorz * playerSpeedHorz).ToHorizontal() + (-Util.Gravity) * playerVelocity.VerticalComponent();
		}

	}

	// Calculates the displacement required in order not to be in a world collider
	protected Vector3 ResolveCollisions(ref Vector3 playerVelocity, bool skipGroundingEntity) {
		// Get nearby colliders
		int numColliders = Physics.OverlapSphereNonAlloc(transform.position, Radius + 0.1f,
			_overlappingColliders, ~_excludedLayers, QueryTriggerInteraction.Ignore);

		var totalDisplacement = Vector3.zero;
		var checkedColliderIndices = new HashSet<int>();

		// If the player is intersecting with that environment collider, separate them
		for (var i = 0; i < numColliders; i++) {
			// Two player colliders shouldn't resolve collision with the same environment collider
			if (checkedColliderIndices.Contains(i)) {
				continue;
			}

			var envColl = _overlappingColliders[i];

			// Skip empty slots
			if (envColl == null || envColl.isTrigger || envColl == m_pCollidersThis) {
				continue;
			}

			//if(envColl.gameObject.layer == 0)
			//	Debug.Log("ResolveCollisions on " + name + " hit a default layer obj: " + envColl.name + "'s collider of type " + envColl.ToString() + " while self collider is " + m_pCollidersThis.ToString());

			Vector3 collisionNormal;
			float collisionDistance;
			if (Physics.ComputePenetration(
				_collisionVolume, _collisionVolume.transform.position, _collisionVolume.transform.rotation,
				envColl, envColl.transform.position, envColl.transform.rotation,
				out collisionNormal, out collisionDistance)) {
				// Ignore very small penetrations
				// Required for standing still on slopes
				// ... still far from perfect though
				if (collisionDistance < _slopePenetrationIgnoreLimit) {
					continue;
				}

				// Skip clipping our velocity if we have a grounding velocity clip pending - fixes bouncing when hitting a surface moving downwards
				if (skipGroundingEntity && m_groundingEntity == envColl.transform) {
					continue;
				}

				checkedColliderIndices.Add(i);

				// Shift out of the collider
				totalDisplacement += collisionNormal * collisionDistance;

				// Clip down the velocity component which is in the direction of penetration
				playerVelocity -= Vector3.Project(playerVelocity, collisionNormal);
			}
		}

		// It's better to be in a clean state in the next resolve call
		for (var i = 0; i < numColliders; i++) {
			_overlappingColliders[i] = null;
		}

		return totalDisplacement;
	}

	// If one of the rays hit, we're considered to be grounded
	protected bool IsGrounded(out Vector3 groundNormal) {
		// If vertical speed (w.r.t. gravity direction) is greater than our jump velocity, don't ground us.
		// This allows for sliding up ramps when moving fast
		// If we allow downward slide, the player can slide when their vertical velocity is downward
		float verticalSpeed = _velocity.VerticalComponent();
		bool isGrounded = false;
		groundNormal = -Util.Gravity;

		// Prevent grounding while moving upwards, unless we're boarding. This will need changing if jump-while-boarding is allowed
		if ((verticalSpeed > rampThreshold && !m_boardEnabled) || (beingLaunched && verticalSpeed > 0)) {
			return false;
		} else {
			foreach (var t in _groundedRayPositions) {
				// The last one is reserved for ghost jumps
				// Don't check that one if already on the ground
				// Ghost jump is typically behind the player and below so that we can check if we've recently jumped etc.
				if (t == _ghostJumpRayPosition && isGrounded) {
					continue;
				}

				RaycastHit[] hits = Physics.RaycastAll(t.position, Util.Gravity, 0.51f, ~_excludedLayers, QueryTriggerInteraction.Ignore);
				//RaycastHit hit;
				//if (Physics.Raycast(t.position, Gravity.Down, out hit, 0.51f, ~_excludedLayers, QueryTriggerInteraction.Ignore)) {
				foreach (RaycastHit hit in hits) {
					if (hit.collider == m_pCollidersThis)
						continue;

					if (Vector3.Angle(-Util.Gravity, hit.normal) <= maxSlopeAngle) {
						groundNormal = hit.normal;
						m_groundingEntity = hit.transform;

						return true;
					}
				}
			}
		}
		return isGrounded;
	}

	// If there's something between the current position and the next, clamp displacement
	protected void ClampDisplacement(ref Vector3 playerVelocity, ref Vector3 displacement, Vector3 playerPosition) {
		RaycastHit[] hits = Physics.RaycastAll(playerPosition, playerVelocity.normalized, displacement.magnitude, ~_excludedLayers);
		foreach (RaycastHit hit in hits) {
			if (hit.collider != m_pCollidersThis) {
				displacement = hit.point - playerPosition;
				return;
			}
		}
	}

	protected void ClipGroundingVelocity() {
		/*Vector3 velocity_platform = _velocity_ws - _velocity;
		float dotProd = Vector3.Dot(velocity_platform.normalized, _velocity.normalized);
		Vector3 velocityToRemove = velocity_platform * dotProd;

		SetVelocity(_velocity - velocityToRemove);*/

		Vector3 velocityFromParent_ws = GetFakeParentMotionAsVelocity();
		float dotProd = Vector3.Dot(velocityFromParent_ws.normalized, _velocity.normalized);
		Vector3 velocityToRemove = velocityFromParent_ws * dotProd;

		SetVelocity(_velocity - velocityToRemove);
	}

	protected void CheckGroundingParentConstraint() {
		// We are grounded or ungrounded this tick
		if (_isGrounded ^ _isGroundedInPrevFrame) {
			if (_isGrounded) {
				SetFakeParent(m_groundingEntity, EFakeParentType.Ground);
			} else {
				SetFakeParent(null, EFakeParentType.Ground);
			}
		}
	}

	protected Vector3 ApplyFakeParentTransform(Vector3 playerPosition_ws) {
		if (!m_hasFakeParent) return playerPosition_ws;

		// DBG
		Vector3 dbgPreviousPos = playerPosition_ws;

		// Get change in rotation of fake parent
		Quaternion rot_delta = m_fakeParentEntity.rotation * Quaternion.Inverse(m_fakeParentRotationPrevious);

		// Rotate us
		Vector3 rot_delta_yawOnly = rot_delta.eulerAngles;
		rot_delta_yawOnly.x = rot_delta_yawOnly.z = 0.0f;

		transform.Rotate(rot_delta_yawOnly, Space.World);

		// Get the vector of distance from fake parent to us and rotate it by change in parent rotation
		playerPosition_ws = rot_delta * (playerPosition_ws - m_fakeParentPositionPrevious) + m_fakeParentPositionPrevious;

		// Move by fake parent translation
		playerPosition_ws += m_fakeParentEntity.position - m_fakeParentPositionPrevious;

		// Moved this to where this method is called in main update; dont want to update these values otherwise in GetFakeParentMotionAsVelocity
		//m_fakeParentRotationPrevious = m_fakeParentEntity.rotation;
		//m_fakeParentPositionPrevious = m_fakeParentEntity.position;

		return playerPosition_ws;
	}

	protected Vector3 GetFakeParentMotionAsVelocity() {
		if (!m_hasFakeParent) return Vector3.zero;

		// The amount we would have moved this frame if we were fake-parented to the entity
		Vector3 projectedPlayerPos_ws = ApplyFakeParentTransform(transform.position);

		// The total translation vector we would have for this second (i.e. velocity):
		return (projectedPlayerPos_ws - transform.position) / Time.fixedDeltaTime;
	}

	protected void ApplyFakeParentMotionAsVelocity() {
		if (!m_hasFakeParent) return;

		// Add that to the velocity
		_velocity += GetFakeParentMotionAsVelocity();
	}

	// Handy when testing
	public void ResetAt(Transform t) {
		transform.position = t.position + Vector3.up * 0.5f;
		//_camTransform.position = _defaultCamPos;
		_velocity = t.TransformDirection(Vector3.forward);
	}

	public void AddForce(Vector3 force, bool explosion = false) {
		//_velocity += force;
		//velocityForNextFrame += _velocity + force;
		//velocityForNextFrame += force;
		_velocity += force;
		beingLaunched = true;
	}

	public void MakeInput(ControllerInputs type, Vector3 value) {
	}

	public void MakeInput(ControllerInputs type, Vector2 value) {
		switch (type) {
			case ControllerInputs.LOOK:
				m_Look = value;
				break;
			case ControllerInputs.MOVE:
				if (m_currentState == KinematicController.EControllerState.Seated)
					return;
				m_Move = value;
				break;
			case ControllerInputs.MOVEWORLDSPACE:
				if (m_currentState == KinematicController.EControllerState.Seated)
					return;
				Vector3 inLocalSpace = _camTransform.InverseTransformDirectionHorizontal(new Vector3(value.x, 0f, value.y));
				m_Move = new Vector2(inLocalSpace.x, inLocalSpace.z);
				break;
		}
	}

	public void MakeInput(ControllerInputs type, float value) {
	}

	public void MakeInput(ControllerInputs type, bool value) {
		switch (type) {
			case ControllerInputs.JUMP:
				if (m_currentState == KinematicController.EControllerState.Seated)
					return;
				_isGonnaJump = value && !m_boardEnabled; // Can't jump while boarding
				break;
		}
	}

	public void MakeInput(ControllerInputs type) {
		switch (type) {
			case ControllerInputs.BOARD:
				if (m_currentState == KinematicController.EControllerState.Seated)
					return;
				m_boardEnabled = !m_boardEnabled;
				_isGonnaJump &= !m_boardEnabled;
				break;
		}
	}

	public void Push(Vector3 pushVector, Vector3? point = null) {
		AddForce(pushVector);
	}

	public void OnThirdPersonEntered() {
		ResetView();
		m_camera.enabled = false;
	}

	public void OnThirdPersonExited() {
		ResetView();
		m_camera.enabled = true;
	}

	void ResetView() {
		_yaw = 0.0f;
		_pitch = 0.0f;
		_camTransform.localRotation = Quaternion.identity;
		transform.localRotation = Quaternion.identity;
	}

	private void OnGUI() {
		float yPos = 1;
		float xPos = 1920 - 500;
		const float padding = 0.2f;
		const float height = 35.0f;
		GUI.color = new Color(.5f, .1f, .1f, 1.0f);
		GUI.Label(new Rect(xPos, yPos += padding + height, 150, height), "Grounded: " + (_isGrounded ? "True" : "False"));
		GUI.Label(new Rect(xPos, yPos += padding + height, 150, height), "Grounding Entity: " + (m_groundingEntity != null ? "True" : "False"));
		GUI.Label(new Rect(xPos, yPos += padding + height, 150, height), "Velocity: " + _velocity.ToString("F2") + " -> " + _velocity.magnitude.ToString("F2"));
		GUI.Label(new Rect(xPos, yPos += padding + height, 150, height), "H. Speed: " + _velocity.horizontalMagnitude().ToString("F2"));
		GUI.Label(new Rect(xPos, yPos += padding + height, 150, height), "Motion From Parent: " + dbg_motionFromFakeParent.ToString("F2"));
		GUI.Label(new Rect(xPos, yPos += padding + height, 150, height), "Velocity Clip Pending: " + (m_groundingVelocityClipPending ? "True" : "False"));
		GUI.Label(new Rect(xPos, yPos += padding + height, 150, height), "Boarding: " + (m_boardEnabled ? "True" : "False"));
	}
}
