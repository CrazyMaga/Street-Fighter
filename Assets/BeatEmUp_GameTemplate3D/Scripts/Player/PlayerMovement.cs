using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnitState))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour {

	[Header("Linked Components")]
	private UnitAnimator animator;
	private Rigidbody rb;
	private UnitState playerState;
	private CapsuleCollider capsule;

	[Header("Settings")]
	public float walkSpeed = 3f;
	public float ZSpeed = 1.5f;
	public float JumpForce = 8f;
	public bool AllowDepthJumping;
	public float AirAcceleration = 3f;
	public float AirMaxSpeed = 3f;
	public float rotationSpeed = 15f;
	public float jumpRotationSpeed = 30f;
	public float lookAheadDistance = .2f;
	public float landRecoveryTime = .1f;
	public float landTime = 0;
	public LayerMask CollisionLayer;

	[Header("Audio")]
	public string jumpUpVoice = "";
	public string jumpLandVoice = "";

	[Header("Stats")]
	public DIRECTION currentDirection;
	public Vector2 inputDirection;
	public bool jumpInProgress;

	private bool isDead = false;
	private bool JumpNextFixedUpdate;
	private float jumpDownwardsForce = .3f;

	//a list of states that this component can influence
	private List<UNITSTATE> MovementStates = new List<UNITSTATE> {
		UNITSTATE.IDLE,
		UNITSTATE.WALK,
		UNITSTATE.JUMPING,
		UNITSTATE.JUMPKICK,
		UNITSTATE.LAND,
	};

	//--

	void OnEnable() {
		InputManager.onCombatInputEvent += InputEventAction;
		InputManager.onInputEvent += InputEvent;
	}

	void OnDisable() {
		InputManager.onCombatInputEvent -= InputEventAction;
		InputManager.onInputEvent -= InputEvent;
	}

	void Start(){

		//find components
		if(!animator) animator = GetComponentInChildren<UnitAnimator>();
		if(!rb) rb = GetComponent<Rigidbody>();
		if(!playerState) playerState = GetComponent<UnitState>();
		if(!capsule) capsule = GetComponent<CapsuleCollider>();

		//error messages for missing components
		if(!animator) Debug.LogError("No animator found inside " + gameObject.name);
		if(!rb) Debug.LogError("No Rigidbody component found on " + gameObject.name);
		if(!playerState) Debug.LogError("No UnitState component found on " + gameObject.name);
		if(!capsule) Debug.LogError("No Capsule Collider found on " + gameObject.name);
	}

	void FixedUpdate() {
		if(!MovementStates.Contains(playerState.currentState) || isDead) return;

		//start a jump
		if(JumpNextFixedUpdate){
			Jump();
			return;
		}

		//land after a jump
		if(jumpInProgress && IsGrounded()){ 
			HasLanded();
			return;
		}

		//continue after landing
		if(playerState.currentState == UNITSTATE.LAND && Time.time - landTime > landRecoveryTime) playerState.SetState(UNITSTATE.IDLE);

		//air and ground Movement
		bool isGrounded = IsGrounded();
		animator.SetAnimatorBool("isGrounded", isGrounded);
		if(isGrounded){
			MoveGrounded();
		} else {
			MoveAirborne();
		}

		//always turn towards the current direction
		TurnToCurrentDirection();
	}

	//movement on the ground
	void MoveGrounded(){

		//do nothing when landing
		if(playerState.currentState == UNITSTATE.LAND) return;

		//set rigidbody velocity
		if(rb != null && (inputDirection.sqrMagnitude>0 && !WallInFront())) {
			rb.velocity = new Vector3( inputDirection.x * -walkSpeed, rb.velocity.y + Physics.gravity.y * Time.fixedDeltaTime, inputDirection.y * -ZSpeed);
			if(animator) animator.SetAnimatorFloat("MovementSpeed", rb.velocity.magnitude);
			playerState.SetState(UNITSTATE.WALK);

		} else {
			rb.velocity = new Vector3(0, rb.velocity.y + Physics.gravity.y * Time.fixedDeltaTime, 0);
			if(animator) animator.SetAnimatorFloat("MovementSpeed", 0);
			playerState.SetState(UNITSTATE.IDLE);
		}
	}

	//movement in the air
	void MoveAirborne(){

		//falling down
		if(rb.velocity.y < 0.1f && playerState.currentState != UNITSTATE.KNOCKDOWN)	animator.SetAnimatorBool("Falling", true);

		if(!WallInFront()) {

			//movement direction based on current input
			int dir = Mathf.Clamp(Mathf.RoundToInt(-inputDirection.x), -1, 1);
			float xpeed = Mathf.Clamp(rb.velocity.x + AirMaxSpeed * dir * Time.fixedDeltaTime * AirAcceleration, -AirMaxSpeed, AirMaxSpeed);
			float downForce = rb.velocity.y>0? 0 : jumpDownwardsForce; //adds a small downwards force when going down 

			//apply movement
			if(AllowDepthJumping) {
				rb.velocity = new Vector3(xpeed, rb.velocity.y - downForce, -inputDirection.y * ZSpeed);
			} else {
				rb.velocity = new Vector3(xpeed, rb.velocity.y - downForce, 0);
			}
		}
	}

	//perform a jump
	void Jump(){
		playerState.SetState(UNITSTATE.JUMPING);
		JumpNextFixedUpdate = false;
		jumpInProgress = true;
		rb.velocity = Vector3.up * JumpForce;

		//play animation
		animator.SetAnimatorBool("JumpInProgress", true);
		animator.SetAnimatorTrigger("JumpUp");
		animator.ShowDustEffectJump();

		//play sfx
		if(jumpUpVoice != "") GlobalAudioPlayer.PlaySFXAtPosition(jumpUpVoice, transform.position);
	}

	//player has landed after a jump
	void HasLanded(){
		jumpInProgress = false;
		playerState.SetState(UNITSTATE.LAND);
		rb.velocity = Vector2.zero;
		landTime = Time.time;

		//set animator properties
		animator.SetAnimatorFloat("MovementSpeed", 0f);
		animator.SetAnimatorBool("JumpInProgress", false);
		animator.SetAnimatorBool("JumpKickActive", false);
		animator.SetAnimatorBool("Falling", false);
		animator.ShowDustEffectLand();

		//sfx
		GlobalAudioPlayer.PlaySFX("FootStep");
		if(jumpLandVoice != "") GlobalAudioPlayer.PlaySFXAtPosition(jumpLandVoice, transform.position);
	}

	#region controller input

	//set current direction to input direction
	void InputEvent(Vector2 dir) {
		
		//ignore input when we are dead or when this state is not active
		if(!MovementStates.Contains(playerState.currentState) || isDead) return;

		//set current direction based on the input vector. Mathf.sign is used because we want the player to stay in the left or right direction when moving up/down)
		int dir2 = Mathf.RoundToInt(Mathf.Sign((float)-inputDirection.x));
		if(Mathf.Abs(inputDirection.x) > 0) SetDirection((DIRECTION)dir2);
		inputDirection = dir;
	}

	//input actions
	void InputEventAction(INPUTACTION action) {

		//ignore input when we are dead or when this state is not active
		if(!MovementStates.Contains(playerState.currentState) || isDead) return;

		//start a jump
		if(action == INPUTACTION.JUMP && IsGrounded() && playerState.currentState != UNITSTATE.JUMPING) JumpNextFixedUpdate = true;
	}

	#endregion
		
	//interrups an ongoing jump
	public void CancelJump(){
		jumpInProgress = false;
	}
		
	//set current direction
	public void SetDirection(DIRECTION dir) {
		currentDirection = dir;
		if(animator) animator.currentDirection = currentDirection;
	}

	//returns the current direction
	public DIRECTION getCurrentDirection() {
		return currentDirection;
	}

	//returns true if the player is grounded
	public bool IsGrounded() {

		//check for capsule collisions with a 0.1 downwards offset from the capsule collider
		Vector3 bottomCapsulePos = transform.position + (Vector3.up) * (capsule.radius - 0.1f);
		return Physics.CheckCapsule(transform.position + capsule.center, bottomCapsulePos, capsule.radius, CollisionLayer);
	}
		
	//look (and turns) towards a direction
	public void TurnToCurrentDirection() {
		if(currentDirection == DIRECTION.Right || currentDirection == DIRECTION.Left) {
			float turnSpeed = jumpInProgress? jumpRotationSpeed : rotationSpeed;
			Vector3 newDir = Vector3.RotateTowards(transform.forward, Vector3.forward * -(int)currentDirection, turnSpeed * Time.fixedDeltaTime, 0.0f);
			transform.rotation = Quaternion.LookRotation(newDir);
		}
	}

	//update the direction based on the current input
	public void updateDirection() {
		TurnToCurrentDirection();
	}

	//the player has died
	void Death() {
		isDead = true;
		rb.velocity = Vector3.zero;
	}

	//returns true if there is a environment collider in front of us
	bool WallInFront() {
		var MovementOffset = new Vector3(inputDirection.x, 0, inputDirection.y) * lookAheadDistance;
		var c = GetComponent<CapsuleCollider>();
		Collider[] hitColliders = Physics.OverlapSphere(transform.position + Vector3.up * (c.radius + .1f) + -MovementOffset, c.radius, CollisionLayer);

		int i = 0;
		bool hasHitwall = false;
		while(i < hitColliders.Length) {
			if(CollisionLayer == (CollisionLayer | 1 << hitColliders[i].gameObject.layer)) hasHitwall = true;
			i++;
		}
		return hasHitwall;
	}

	//draw a lookahead sphere in the unity editor
	#if UNITY_EDITOR
	void OnDrawGizmos() {
		var c = GetComponent<CapsuleCollider>();
		Gizmos.color = Color.yellow;
		Vector3 MovementOffset = new Vector3(inputDirection.x, 0, inputDirection.y) * lookAheadDistance;
		Gizmos.DrawWireSphere(transform.position + Vector3.up * (c.radius + .1f) + -MovementOffset, c.radius);
	}
	#endif
}

public enum DIRECTION {
	Right = -1,
	Left = 1,
	Up = 2,
	Down = -2,
};