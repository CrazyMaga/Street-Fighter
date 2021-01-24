using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Animator))]
public class UnitAnimator : MonoBehaviour {

	[HideInInspector]
	public DIRECTION currentDirection;

	[Header ("Effects")]
	public GameObject DustEffectLand;
	public GameObject DustEffectJump;
	public GameObject HitEffect;
	public GameObject DefendEffect;
	[HideInInspector]
	public Animator animator;
	private bool isplayer;

	//awake
	void Awake() {
		if(animator == null) animator = GetComponent<Animator>();
		isplayer = transform.parent.CompareTag("Player");
		currentDirection = DIRECTION.Right;
	}

	//play an animation
	public void SetAnimatorTrigger(string triggerName) {
		animator.SetTrigger(triggerName);
	}

	//sets a bool in the animator
	public void SetAnimatorBool(string name, bool state){
		animator.SetBool(name, state);
	}

	//sets a float in the animator
	public void SetAnimatorFloat(string name, float value){
		animator.SetFloat (name, value);
	}

	//set a direction
	public void SetDirection(DIRECTION dir){
		currentDirection = dir;
	}
		
	//------------------------
	//--- Animation Events ---
	//-------------------------

	//Animation has finished playing, unit is ready for new input
	public void Ready() {
		if (isplayer) {
			transform.parent.GetComponent<PlayerCombat>().Ready ();
		} else {
			transform.parent.GetComponent<EnemyAI>().Ready();
		}
	}

	//check if something was hit
	public void Check4Hit() {

		//check if the player has hit something
		if (isplayer) {
			PlayerCombat playerCombat = transform.parent.GetComponent<PlayerCombat> ();
			if (playerCombat != null) {
				playerCombat.CheckForHit();
			} else {
				Debug.Log ("no player combat component found on gameObject '" + transform.parent.name + "'.");
			}
			
		} else {

			//check if the enemy has hit something
			EnemyAI AI = transform.parent.GetComponent<EnemyAI>();
			if (AI != null) {
				AI.CheckForHit();
			} else {
				Debug.Log ("no enemy AI component found on gameObject '" + transform.parent.name + "'.");
			}
		}
	}

	//show hit effect
	public void ShowHitEffect() {
		float unitHeight = 1.6f;
		GameObject.Instantiate (HitEffect, transform.position+Vector3.up * unitHeight, Quaternion.identity);
	}

	//show defend effect
	public void ShowDefendEffect() {
		GameObject.Instantiate(DefendEffect, transform.position + Vector3.up * 1.3f, Quaternion.identity);
	}

	//Show dust effect
	public void ShowDustEffectLand() {
		GameObject.Instantiate (DustEffectLand, transform.position + Vector3.up * .13f , Quaternion.identity);
	}

	//Show jump dust effect
	public void ShowDustEffectJump() {
		GameObject.Instantiate (DustEffectJump, transform.position + Vector3.up * .13f , Quaternion.identity);
	}

	//play audio
	public void PlaySFX(string sfxName) {
		GlobalAudioPlayer.PlaySFXAtPosition(sfxName, transform.position + Vector3.up);
	}

	//adds a small forward force
	public void AddForce(float force) {
		StartCoroutine (AddForceCoroutine(force));
	}

	//adds small force over time
	IEnumerator AddForceCoroutine(float force) {
		DIRECTION startDir = currentDirection;
		Rigidbody rb = transform.parent.GetComponent<Rigidbody>();
		float speed = 8f;
		float t = 0;

		while (t < 1) {
			yield return new WaitForFixedUpdate();
			rb.velocity = Vector2.right * (int)startDir * Mathf.Lerp (force, rb.velocity.y, MathUtilities.Sinerp (0, 1, t));
			t += Time.fixedDeltaTime * speed;
			yield return null;
		}
	}

	//flicker effect
	public IEnumerator FlickerCoroutine(float delayBeforeStart){
		yield return new WaitForSeconds (delayBeforeStart);

		//find all renderers inside this gameObject
		Renderer[] CharRenderers = GetComponentsInChildren<Renderer>();

		if(CharRenderers.Length > 0) {
			float t = 0;
			while(t < 1) {
				float speed = Mathf.Lerp(15, 35, MathUtilities.Coserp(0, 1, t));
				float i = Mathf.Sin(Time.time * speed);
				foreach(Renderer r in CharRenderers)
					r.enabled = i > 0;
				t += Time.deltaTime / 2;
				yield return null;
			}
			foreach(Renderer r in CharRenderers)
				r.enabled = false;
		}
		Destroy(transform.parent.gameObject);
	}

	//camera shake
	public void CamShake(float intensity){
		CamShake camShake = Camera.main.GetComponent<CamShake> ();
		if (camShake != null)
			camShake.Shake (intensity);
	}

	//spawn a projectile
	public void SpawnProjectile(string name){
		
		PlayerCombat playerCombat = transform.parent.GetComponent<PlayerCombat>();
		if(playerCombat){

			//find a custom spawn position, if there is any. Otherwise use the weapon hand as spawn position
			Vector3 spawnPos = playerCombat.weaponBone.transform.position;
			ProjectileSpawnPos customSpawnPos = playerCombat.weaponBone.GetComponentInChildren<ProjectileSpawnPos>();
			if(customSpawnPos) spawnPos = customSpawnPos.transform.position;

			//spawn projectile at spawn position
			GameObject projectilePrefab = GameObject.Instantiate(Resources.Load(name), spawnPos, Quaternion.identity) as GameObject;
			if(!projectilePrefab) return;

			//set projectile to current direction
			Projectile projectileComponent = projectilePrefab.GetComponent<Projectile>();
			if(projectileComponent){
				projectileComponent.direction = playerCombat.currentDirection;
				Weapon currentWeapon = playerCombat.GetCurrentWeapon();
				if(currentWeapon != null) projectileComponent.SetDamage(playerCombat.GetCurrentWeapon().damageObject);
			}
		}
	}
}