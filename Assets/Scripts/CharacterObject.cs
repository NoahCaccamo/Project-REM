using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;


// check if this is ok
using static InputBuffer;

// https://youtu.be/zAIdyNDBe0A?si=7HMFTwJ7_uMWFrty&t=1758

public class CharacterObject : MonoBehaviour, IEffectable
{
    public StatusEffectData _data;

    public InventoryObject inventory;

    public float hp = 10f;

    public Vector3 velocity;

    public Vector3 gravity = new Vector3(0, -0.01f, 0);

    // higher is more dampening
    public Vector3 friction = new Vector3(0.95f, 0.99f, 0.95f);

    public CharacterController myController;

    public int currentState;
    public float currentStateTime;
    public float prevStateTime;

    public GameObject character;
    public GameObject draw;

    public Animator myAnimator;

    public enum ControlType {  AI, PLAYER }
    public ControlType controlType;

    public Hitbox hitbox;

    public bool canCancel;
    public int hitConfirm;

    public InputBuffer inputBuffer = new InputBuffer();

    public bool targeting = false;
    public GameObject target;
    public GameObject softTarget = null;

    public bool invulnerable = false;

    public bool faceStick = false;

    public Transform _playerTrans;

    public LayerMask whatIsPlayer;

    public float localTimescale = 1f;

    public GameObject bullet;
    public GameObject enemyDrop;
    public bool overclocked = false;
    public float overclockDrainRate = 0.1f;

    // TEMP FOR TESTING
    public RoomManager roomManager;
    public Camera minigameCam;

    public IControlStrategy controlStrategy;
    // public EnemyAIBehaviour aiBehaviour;
    // public EnemyData enemyData;

    public PetAIBehaviour petBehaviour;
    public PetData petData;

    public bool hasArmor = false;
    public float armorHealth = 100f;
    public bool isArmorBroken = false;

    public BreakerInventoryObject breakerInventory;

    public bool canWallBounce = false;

    void Start()
    {
        wallLayerMask = LayerMask.GetMask("Wall");
        myController = GetComponent<CharacterController>();
        // myAnimator = GetComponent<Animator>();

        // Set up our enemy AI's data
        if (controlType == ControlType.AI)
        {
            controlStrategy = new PetAIControl(petBehaviour);
            
            if (petBehaviour != null)
            {
                petBehaviour.Initialize(this);
            }

            // hp = enemyData.hp;

            if (_playerTrans == null)
            {
                _playerTrans = GameEngine.gameEngine.mainCharacter.transform;
            }

            enemyLayerMask = LayerMask.GetMask("Player");
        } else
        {
            enemyLayerMask = LayerMask.GetMask("Enemy");
        }
    }

    // INVENTORY STUFF
    public void OnTriggerEnter(Collider other)
    {
        var item = other.GetComponent<Item>();
        if (item)
        {
            inventory.AddItem(item.item, 1);
            Destroy(other.gameObject);
        }

        var _roomManager = other.GetComponent<RoomManager>();
        if (_roomManager)
        {
            roomManager = _roomManager;
        }
    }
    private void OnApplicationQuit()
    {
        inventory.Container.Clear();
    }
    // END INVENTORY STUFF

    public float atkCooldown = 60;


    void Update()
    {
        switch (controlType)
        {
            case ControlType.AI:
                controlStrategy?.Tick(this);
                break;

            case ControlType.PLAYER:
                UpdateInput();
                break;
        }

        if (GameEngine.hitStop <= 0)
        {

            softTarget = TargetClosestEnemy();
            // UpdateInputBuffer

            // Update Input
            

            // Update State Machine
            UpdateState();

            // Update physics
            UpdatePhysics();

            if (_data != null) HandleEffect();
        }
        UpdateTimers();
        UpdateAnimation();
    }

    void LateUpdate()
    {
        if (controlType == ControlType.PLAYER)
        {
            ResolveEnemyOverlap();
            ManualPushback();
        }
    }

    bool IsPushingEnemy(Vector3 moveDir)
    {
        RaycastHit hit;
        return Physics.SphereCast(transform.position, 0.4f, moveDir, out hit, 0.45f, enemyLayerMask);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z), 0.6f);
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y - 0.6f, transform.position.z), 0.6f);

        Gizmos.color = Color.blue;

        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.45f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.45f);

    }

    private void ManualPushback()
    {
        Collider[] hits = Physics.OverlapCapsule(
    transform.position + Vector3.up * 0.5f,
    new Vector3(transform.position.x, transform.position.y, transform.position.z),
    0.45f,
    enemyLayerMask
);

        foreach (var hit in hits)
        {
            Vector3 toPlayer = transform.position - hit.transform.position;
            toPlayer.y = 0;
            float distance = toPlayer.magnitude;
            float pushDistance = 0.8f; // Desired minimum spacing

            if (distance < pushDistance && distance > 0.01f)
            {
                Vector3 pushDir = toPlayer.normalized;
                float correction = pushDistance - distance;

                // Push the player back
                myController.Move(pushDir * correction);
                // Enemy
                if (hit.TryGetComponent<CharacterObject>(out var enemy))
                {
                    enemy.velocity += -pushDir * correction * 0.2f;
                }

            }
        }

    }

    private void ResolveEnemyOverlap2()
    {
        Collider[] overlaps = Physics.OverlapCapsule(new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z), new Vector3(transform.position.x, transform.position.y - 0.6f, transform.position.z), 0.6f, enemyLayerMask);

        foreach (var col in overlaps)
        {
            Vector3 toEnemy = transform.position - col.transform.position;


            // raycast instead??
            bool isAbove = toEnemy.y > 0.3f; // Tweak this threshold

            if (isAbove)
            {
                Vector3 horizontalPush = new Vector3(toEnemy.x, 0, toEnemy.z).normalized * 0.05f;
                myController.Move(horizontalPush);
            }

        }
    }




    // may need to clamp enemy velocity or use vector3 smoothdamp if jerky
    private void ResolveEnemyOverlap()
    {
        Collider[] overlaps = Physics.OverlapCapsule(new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z), new Vector3(transform.position.x, transform.position.y - 0.6f, transform.position.z), 0.6f, enemyLayerMask);

        foreach (var col in overlaps)
        {
            if (col.TryGetComponent<CharacterObject>(out var enemy))
            {
                Vector3 offset =  transform.position - col.transform.position;
                float verticalDiff = offset.y;
                Vector3 horizontalOffset = new Vector3(offset.x, 0f, offset.z);

                // handled in other function now
                /*
                // Push enemy sideways if player is not directly above
                if (Mathf.Abs(verticalDiff) < 1.0f) // Rough check for "not above"
                {
                    Vector3 pushDir = -horizontalOffset.normalized;

                    // Slight push force
                    enemy.velocity += pushDir * 0.03f;
                }
                */

                // If player is above (falling on enemy), push player off
                if (verticalDiff > 0.3f)
                {
                    Vector3 shoveDir = horizontalOffset.normalized;
                    myController.Move(shoveDir * 0.08f);
                }
            }
        }
    }

    void UpdateTimers()
    {
        if (dashCooldown > 0) { dashCooldown -= dashCooldownRate * 60 * Time.deltaTime * localTimescale; }
        if (overclocked)
        {
            if (specialMeter > 0)
            {
                ChangeMeter(-overclockDrainRate * 60 * Time.deltaTime);
                localTimescale = 2f;
                if (hp < 20)
                {
                    hp += (overclockDrainRate * 0.02f) * 60 * Time.deltaTime;
                }
            }
            else
            {
                ExitOverclocked();
            }
        }
        if (noGrav > 0) { noGrav -= Time.deltaTime * 60 * localTimescale; }
    }

    void ExitOverclocked()
    {
        hasArmor = false;
        localTimescale = 1f;
        overclocked = false;
    }

    public float aniMoveSpeed;
    public float aniSpeed;
    void UpdateAnimation()
    {
        aniSpeed = localTimescale;

        if (GameEngine.hitStop > 0) { aniSpeed = 0; }
        Vector3 latspeed = new Vector3(velocity.x, 0, velocity.z);
        aniMoveSpeed = Vector3.SqrMagnitude(latspeed) * 30f; // factor of 30 can change later
        aniFallSpeed = velocity.y * 30f;
        myAnimator.SetFloat("moveSpeed", aniMoveSpeed);
        myAnimator.SetFloat("aerialState", aniAerialState);
        myAnimator.SetFloat("fallSpeed", aniFallSpeed);

        // NOTE: Each hit animation can be one singular frame per direction that we can blend into
        // Hit left, right, up and down = all 4 directions
        myAnimator.SetFloat("hitAniX", currHitAni.x);
        myAnimator.SetFloat("hitAniY", currHitAni.y);
        myAnimator.SetFloat("aniSpeed", aniSpeed);
    }

    public Vector2 leftStick;
    void CameraRelativeStickMove(float _val)
    {
        Vector3 velHelp = new Vector3(0, 0, 0);
        Vector3 velDir;

        //leftStick = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (leftStick.x > deadzone || leftStick.x < -deadzone || leftStick.y > deadzone || leftStick.y < -deadzone)
        {
            // this helps correct some incorrect values. Errs if normalized all the time tho so that why check
            if (leftStick.sqrMagnitude > 1) { leftStick.Normalize(); }

            velDir = Camera.main.transform.forward;
            velDir.y = 0;
            velDir.Normalize();
            velHelp += velDir * leftStick.y;

            velHelp += Camera.main.transform.right * leftStick.x;
            velHelp.y = 0;


            velHelp *= _val;

            if (targeting) { velHelp *= 0.5f; }

            if (IsPushingEnemy(velHelp)) { velHelp *= 0.5f; } // used to be 0.3

            velocity += velHelp * Time.deltaTime * 60 * localTimescale;
        }
    }

    void FaceVelocity()
    {
        if (CheckVelocityDeadZone())
        {
            character.transform.rotation = Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z), Vector3.up);
        }
    }

    public void FaceTarget(Vector3 tarPos)
    {
        Vector3 tarOffset = tarPos - transform.position;
        tarOffset.y = 0;
        character.transform.rotation = Quaternion.LookRotation(tarOffset, Vector3.up);
    }

    public bool CheckVelocityDeadZone()
    {
        if (velocity.x > 0.001f) { return true;  }
        if (velocity.x < -0.001f) { return true; }
        if (velocity.z > 0.001f) { return true; }
        if (velocity.z < -0.001f) { return true; }
        return false;
    }

    public bool aerialFlag;
    public float aerialTimer;

    public float aniAerialState;

    public float aniFallSpeed;

    public int jumps;
    public int jumpMax = 2;

    public float dashCooldown;
    public float dashCooldownRate = 1f;

    public float specialMeter;
    public float specialMeterMax = 100f;


    public void UseMeter(float _val)
    {
        ChangeMeter(-_val);
    }

    public void BuildMeter(float _val)
    {
        if (overclocked)
        {
            return;
        }
        ChangeMeter(_val);
    }

    public void ChangeMeter(float _val)
    {
        specialMeter += _val;
        specialMeter = Mathf.Clamp(specialMeter, 0, specialMeterMax);
    }

    public void BoostOnHit()
    {
        velocity.y += 1f;
    }

    void UpdatePhysics()
    {
        if (noGrav <= 0)
        {
            velocity += gravity * Time.deltaTime * 60 * localTimescale;//* localTimescale;
        }
        myController.Move(velocity * 60 * Time.deltaTime * localTimescale);// * localTimescale);

        velocity.x = Mathf.Lerp(velocity.x, 0, friction.x * Time.deltaTime * 60 * localTimescale);
        velocity.y = Mathf.Lerp(velocity.y, 0, friction.y * Time.deltaTime * 60 * localTimescale);
        velocity.z = Mathf.Lerp(velocity.z, 0, friction.z * Time.deltaTime * 60 * localTimescale);



        //transform.position += velocity;

        // Aerial Check
        if ((myController.collisionFlags & CollisionFlags.Below) != 0)
        {
            aerialFlag = false;
            aerialTimer = 0;
            aniAerialState *= 0.75f; // blend to 0 rather than snap for anim
            velocity.y = 0;
            jumps = jumpMax;
        }
        else
        {
            if (!aerialFlag)
            {
                aerialTimer += Time.deltaTime * 60 * localTimescale;
            }
            if (aerialTimer >= 3)
            {
                aerialFlag = true;
                if (aniAerialState <= 1f)
                {
                    aniAerialState += 0.1f * 60 * Time.deltaTime * localTimescale; // 0.05 is 20 frames i think since 0.1 is 10
                }
                if (jumps == jumpMax) { jumps--; }
            }
        }

        // this is bad and ruins launchers. make event work with dash better
        if (hitStun <= 0)
        {
            if (faceStick)
            {
                return;
            }else if (targeting)
            {
                if (target != null)
                {
                    FaceTarget(target.transform.position);
                }
            } else
            {
                FaceVelocity();
            }
        }

        CheckForLedge();
    }


    float ledgeGrabForwardCheck = 1.5f;
    float ledgeGrabUpCheck = 1.2f;
    float ledgeGrabDownCheck = 2.5f;
    Vector3 ledgeGrabOffset;

    bool isGrabbingLedge = false;
    Vector3 ledgeGrabPoint;



    void CheckForLedge()
    {
        if (!aerialFlag || velocity.y > 0 || isGrabbingLedge)
            return;


        RaycastHit downHit;
        Vector3 lineDownStart = (transform.position + Vector3.up * 1.5f) + character.transform.forward;
        Vector3 lineDownEnd = (transform.position + Vector3.up * 0.7f) + character.transform.forward;
        Physics.Linecast(lineDownStart, lineDownEnd, out downHit, wallLayerMask);
        Debug.DrawLine(lineDownStart, lineDownEnd);

        if (downHit.collider != null)
        {
            RaycastHit fwdHit;
            Vector3 lineFwdStart = new Vector3(transform.position.x, downHit.point.y - 0.1f, transform.position.z);
            Vector3 LineFwdEnd = new Vector3(transform.position.x, downHit.point.y-0.1f, transform.position.z) + character.transform.forward;
            Physics.Linecast(lineFwdStart, LineFwdEnd, out fwdHit, wallLayerMask);
            Debug.DrawLine(lineFwdStart, LineFwdEnd);

            if (fwdHit.collider != null)
            {
                noGrav = 10;
                velocity = Vector3.zero;

                isGrabbingLedge = true;

                Vector3 hangPos = new Vector3(fwdHit.point.x, fwdHit.point.y, fwdHit.point.z);
                Vector3 offset = character.transform.forward * -0.1f + transform.up * -1f;
                hangPos += offset;
                transform.position = hangPos;
                character.transform.forward = -fwdHit.normal;

                StartLedgeGrab();
            }
        }
    }


    // GO INTO SPECIFIC STATE
    void StartLedgeGrab()
    {
        // RESET AERIAL STUFF
        jumps = jumpMax;
        isGrabbingLedge = true;
        velocity = Vector3.zero;

        // limit rotation to y axis only

        // Position player at ledge
       // transform.position = ledgeGrabPoint + ledgeGrabOffset;


        StartState(27); // ledge grab state
        // play animation based on state
    }


    // Still needs to handle getting hit out of ledge grab
    // Also maybe I should lock the player position in case anything nudges the player while hanging
    void LedgeGrab()
    {
        // uneeded cause of state or needed for lockout timer?
        if (!isGrabbingLedge) return;

        // uneeded??? maybe???
        // Disable horizontal movement and gravity
        // velocity = Vector3.zero;
        // transform.position = ledgeGrabPoint + ledgeGrabOffset;

        // Wait for player input
        if (Input.GetButtonDown("Jump"))
        {
            ClimbUpLedge();
        }
        /*
        else if (Input.GetButtonDown("Crouch"))
        {
            DropFromLedge();
        }
        */
    }

    void ClimbUpLedge()
    {
        isGrabbingLedge = false;
        // transform.position = ledgeGrabPoint + ledgeGrabOffset;
        StartState(0);// neutral
        velocity.y = 0.45f;
        // Jump(0.45f);
    }

    void DropFromLedge()
    {
        isGrabbingLedge = false;
        noGrav = 0;
    }





    void UpdateState()
    {
        CharacterState myCurrentState = GameEngine.coreData.characterStates[currentState];

        if ( hitStun > 0)
        {
            GettingHit();
        }
        else

        UpdateStateEvents();
        UpdateStateAttacks();

        prevStateTime = currentStateTime;
        currentStateTime += 60 * Time.deltaTime * localTimescale;

        if (currentStateTime >= myCurrentState.length)
        {
            if (myCurrentState.loop) { LoopState(); }
            else { EndState(); }
        }
    }

    void LoopState()
    {
        currentStateTime = 0;
        //currentState = 0;
        prevStateTime = -1;
    }
    void EndState()
    {
        noGrav = 0;
        isWallSlumped = false;

        currentStateTime = 0;
        currentState = 0;
        prevStateTime = -1;
        StartState(currentState);

        // DESTROY AFTER HITSTUN IS DONE HACKY
        if (hp <= 0)
        {
            OnDeath();
        }
    }

    void OnDeath()
    {
        if (controlType == ControlType.AI)
        {
            roomManager.OnEnemyDeath(this.gameObject);
            // Get a random position (box), then spawn the drop
            int numDrops = Random.Range(3, 10);
            for (int i = 0; i < numDrops; i++)
            {
                Vector3 minPos = new Vector3(-1, -1, -1);
                Vector3 maxPos = new Vector3(1, 1, 1);
                Vector3 randomPos = new Vector3(Random.Range(minPos.x, maxPos.x), Random.Range(minPos.y, maxPos.y), Random.Range(minPos.z, maxPos.z));
                Instantiate(enemyDrop, transform.position + randomPos, transform.rotation);
            }
            Destroy(this.gameObject);
        }

        // GO TO WACKY GAME IF DEAD
        if (controlType == ControlType.PLAYER)
        {
            //Camera.main.enabled = false;
            minigameCam.gameObject.SetActive(true);
            minigameCam.enabled = true;
            Time.timeScale = 0;
        }
    }

    void UpdateStateEvents()
    {
        foreach(StateEvent _ev in GameEngine.coreData.characterStates[currentState].events)
        {

            // <= here might fire things twice on first frame?
            // theres a better way to do this
            //if (prevStateTime <= _ev.start && currentStateTime == _ev.start)

            int _curEv = 0; // for use in future, globalprefab?
            if (_ev.active)
            {
                if (!_ev.hasExecuted)
                {
                    if (_ev.start == _ev.end && currentStateTime >= _ev.start)
                    {
                        DoEventScript(_ev.script, _ev.parameters);
                        _ev.hasExecuted = true;
                    }
                    if (currentStateTime >= _ev.start && currentStateTime <= _ev.end) // after the && is hacky
                    {
                        DoEventScript(_ev.script, _ev.parameters);
                    }
                }
            }
            _curEv++;
        }
    }

    public float hitActive;
    public int currentAttackIndex;

    void UpdateStateAttacks()
    {
        int _cur = 0;
        hitActive = 0;
        foreach (Attack _atk in GameEngine.coreData.characterStates[currentState].attacks)
        {
            // hitconfirm here with sepearate flag if we want to count attack hits rather than skill?
            if (currentStateTime >= _atk.start && prevStateTime < _atk.start) // could do a greater catch window when manipulating timescale
            {
                _atk.hitActive = _atk.length;
                hitbox.transform.localScale = _atk.hitboxScale;
                hitbox.transform.localPosition = _atk.hitboxPos;
                currentAttackIndex = _cur;
            }

            if (currentStateTime >= _atk.start + _atk.length)
            {
                _atk.hitActive = 0;
            }

            //HitCancel
            float cWindow = _atk.start + _atk.cancelWindow;
            if (currentStateTime >= cWindow)
            {
                if (hitConfirm > 0) { canCancel = true; }
            }

            if (currentStateTime >= cWindow + wiffWindow)
            {
                canCancel = true;
            }
            // end hitcancel

            hitActive += _atk.hitActive;
            _cur++;
        }
    }

    public static float wiffWindow = 8f;
    void HitCancel()
    {
        return;
        //if (currentStateTime >= _ev.start && currentStateTime <= Event.end)

        float cWindow = GameEngine.coreData.characterStates[currentState].attacks[currentAttackIndex].start +
            GameEngine.coreData.characterStates[currentState].attacks[currentAttackIndex].cancelWindow;

        if (currentStateTime == cWindow)
        {
            if (hitConfirm > 0) { canCancel = true; }
        }

        if (currentStateTime == cWindow + wiffWindow)
        {
            canCancel = true;
        }
    }

    void DoEventScript(int _index, List<ScriptParameter> _params) // + actIndex + evIndex
    {
        if (_params == null) { return; }
        if (_params.Count <= 0) { return; }
        switch(_index)
        {
            case 0:
                VelocityY(_params[0].val);
                break;
            case 1:
                FrontVelocity(_params[0].val);
                break;
            case 3:
                CameraRelativeStickMove(_params[0].val);
                break;
            case 4:
                GettingHit();
                break;
            case 5:
                GlobalPrefab(_params[0].val);
                break;
            case 6:
                CanCancel(_params[0].val);
                break;
            case 7:
                Jump(_params[0].val);
                break;
            case 8:
                FaceVelocity();
                break;
            case 9:
                FaceStick(_params[0].val);
                break;
            case 10:
                VelocityToTarget(_params[0].val);
                break;
            case 11:
                Invulnerable(_params[0].val);
                break;
            case 12:
                HoldVelocity(_params[0].val);
                break;
            case 13:
                Shoot(_params[0].val, _params[1].val);
                break;
            case 14:
                EnemyStep(_params[0].val);
                break;
            case 15:
                LedgeGrab();
                break;
            case 16:
                NoGrav(_params[0].val);
                break;
            case 17:
                Jumping(_params[0].val, _params[1].val);
                break;
            case 18:
                Slam(_params[0].val);
                break;
            case 19:
                BreakerTransition(_params[0].val);
                break;
            case 20:
                GlobalTimescale(_params[0].val, _params[1].val);
                break;
            case 21:
                TempInstantiateGlobalPrefabInFront(_params[0].val);
                break;
            case 22:
                ThrowSpear();
                break;
            case 23:
                ActivateWallbounce();
                break;
        }
    }
    void ActivateWallbounce()
    {
        RaycastHit fwdHit;
        Vector3 lineFwdStart = new Vector3(transform.position.x, transform.position.y - 0.1f, transform.position.z);
        Vector3 LineFwdEnd = new Vector3(transform.position.x, transform.position.y - 0.1f, transform.position.z) + character.transform.forward;
        Physics.Linecast(lineFwdStart, LineFwdEnd, out fwdHit, wallLayerMask);
        Debug.DrawLine(lineFwdStart, LineFwdEnd);

        if (fwdHit.collider != null)
        {
            WallBounce(fwdHit.point);
        }
    }

    void ThrowSpear()
    {

    }

    void TempInstantiateGlobalPrefabInFront(float _index)
    {
        int idx = Mathf.RoundToInt(_index);
        Instantiate(GameEngine.coreData.globalPrefabs[idx], transform.position + character.transform.forward * 4, character.transform.rotation);
    }

    void GlobalTimescale(float _pow, float _duration)
    {
        roomManager.SlowAllEnemies(_pow);
    }

    void BreakerTransition(float _held)
    {
        if (breakerInventory && breakerInventory.breakerSlots.Count > 0)
        {
            if (_held == 0)
            {
                BreakerObject currBreaker = breakerInventory.GetActiveBreaker();
                StartState(currBreaker.tapState);
            } else
            {
                BreakerObject currBreaker = breakerInventory.GetActiveBreaker();
                StartState(currBreaker.holdState);
                breakerInventory.RemoveActiveBreaker();
            }
            return;
        }

        // if we dont have a breaker just go to neutral
        StartState(0);
    }

    void NoGrav(float _timer)
    {
        noGrav = _timer;
    }

    void EnemyStep(float _pow)
    {
        // RESET AIR ACTIONS FUNCTION HERE
        jumps = jumpMax;
        velocity.y = _pow;
    }

    void VelocityToTarget(float _pow)
    {
        target = TargetClosestEnemy();
        if (target)
        {
            Debug.Log("zoomin");
            Vector3 charLoc = character.transform.position;
            Vector3 enemyLoc = target.transform.position;
            Vector3 targetDir = (enemyLoc - charLoc).normalized;
            velocity += targetDir * _pow * Time.deltaTime * 60 * localTimescale;

            // TEMP TEST - removes player collision and enemy push
            character.gameObject.GetComponentInParent<CapsuleCollider>().excludeLayers = 128;
            character.gameObject.GetComponentInParent<CharacterController>().excludeLayers = 128;
        }
    }

    void FaceStick(float _rate)
    {
        Vector3 velHelp = new Vector3(0, 0, 0);
        Vector3 velDir;
        if (leftStick.x > deadzone || leftStick.x < -deadzone || leftStick.y > deadzone || leftStick.y < -deadzone)
        {
            velDir = Camera.main.transform.forward;
            velDir.y = 0;
            velDir.Normalize();
            velHelp += velDir * leftStick.y;

            velHelp += Camera.main.transform.right * leftStick.x;
            velHelp.y = 0;

            Debug.Log("FACING THE STick");
            faceStick = true;
            character.transform.rotation = Quaternion.Lerp(character.transform.rotation, Quaternion.LookRotation(new Vector3(velHelp.x, 0, velHelp.z), Vector3.up), _rate);
        }

    }

    void Jump(float _pow)
    {
        velocity.y = _pow;
        jumps--;
    }

    void Jumping(float _pow, float _inputIndex)
    {
        if (controlType == ControlType.AI)
        {
            return;
        }
        int idx = Mathf.RoundToInt(_inputIndex);
        if (inputBuffer.buffer[24].rawInputs[idx].hold > 1)
        {
            velocity.y += _pow * Time.deltaTime * 60 * localTimescale;
        }
    }

    void CanCancel(float _val)
    {
        if (_val > 0) { canCancel = true; }
        else { canCancel = false; }
    }

    void GlobalPrefab(float _index)
    {
        GameEngine.GlobalPrefab((int)_index, gameObject);
    }

    void FrontVelocity(float _pow)
    {
        velocity += character.transform.forward * _pow * Time.deltaTime * 60 * localTimescale;
    }

    void StickMove(float _pow)
    {
        float _mov = 0;
        if (Input.GetAxisRaw("Horizontal") > deadzone)
        {
            _mov = 1;
        }

        if (Input.GetAxisRaw("Horizontal") < -deadzone)
        {
            _mov = -1;
        }
        
        velocity.x += _mov * moveSpeed * _pow;
    }

    void VelocityY(float _pow)
    {
        velocity.y = _pow;
    }

    void Invulnerable(float _val)
    {
        if (_val > 0) { invulnerable = true; }
        // also increment time invulnerable and reset when state changes
        // this way perfect dodge can check if player was hit in the first few frames of a dodge
        else { invulnerable = false; }
    }

    float prevHold;
    void HoldVelocity(float _val)
    {
        //playerInputBuffer.buffer[b].rawInputs[i].used
        // HARD CODED
        // CAN WE PASS INPUT USED OR SOMETHING?
        if (inputBuffer.buffer[24].rawInputs[3].hold >= _val && prevHold < _val)
        {
            velocity.y = 0.4f;
        }

        prevHold = inputBuffer.buffer[24].rawInputs[3].hold;
    }

    void Shoot(float _pow, float _inputIndex)
    {
        Vector3 direction;
        Quaternion lookRotation = transform.rotation;
        if (target)
        {
            direction = (target.transform.position - transform.position).normalized;
            lookRotation = Quaternion.LookRotation(direction);
        }
        else if (softTarget)
        {
            direction = (softTarget.transform.position - transform.position).normalized;
            lookRotation = Quaternion.LookRotation(direction);
        }

        string[] inputNames = GameEngine.coreData.GetRawInputNames();

        if (Input.GetButtonDown(inputNames[Mathf.RoundToInt(_inputIndex)]))
        {
            Debug.Log("PEW");
            GameObject newBullet = Instantiate(bullet, transform.position, lookRotation);
            newBullet.GetComponent<Bullet>().lookDirection = lookRotation;
            newBullet.GetComponent<Bullet>().character = this;

            VelocityY(0.1f);
        }
    }

    void Slam(float _nextState)
    {
        int stateIdx = Mathf.RoundToInt(_nextState);
        if (!aerialFlag)
        {
            StartState(stateIdx);
        }
    }

    // This is the old Unity input system
    public float deadzone = 0.2f;

    public float moveSpeed = 0.01f;
    public float jumpPow = 1;

    void StartState(int _newState)
    {
        currentState = _newState;
        prevStateTime = -1;
        currentStateTime = 0;
        canCancel = false;
        invulnerable = false;
        prevHold = 0;

        faceStick = false;

        // Attack
        hitActive = 0;
        hitConfirm = 0;
        isWallSlumped = false;

        // Reset oneshot event flags
        foreach (StateEvent _ev in GameEngine.coreData.characterStates[currentState].events)
        {
            if (_ev.hasExecuted)
            {
                _ev.hasExecuted = false;
            }
        }

        foreach (Attack _atk in GameEngine.coreData.characterStates[currentState].attacks)
        {
            _atk.hitActive = 0;
        }

        SetAnimation(GameEngine.coreData.characterStates[currentState].stateName);

        if (hitStun <= 0 && !targeting ) { FaceStick(1); faceStick = false; }

        // BUGGY AF
        // FIX THIS
        /*
        if (softTarget)
        {
            FaceTarget(softTarget.gameObject.transform.position);
            faceStick = false;
        }
        */
    }

    // externally called from enemy
    public void ChangeState(int _newState)
    {

        OnEnteringState(_newState);
        StartState(_newState);
    }

    // optional hook when changing states
    void OnEnteringState(int _newState)
    {

    }

    public bool IsEnemyCloseToFeet()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(character.transform.position - Vector3.down * 0.4f, 2f, enemyLayerMask);
        foreach (Collider hitEnemy in hitEnemies)
        {
            // check if you can step?

        }
        if (hitEnemies.Length > 0)
        {
            return true;
        }
        return false;
    }

    void SetAnimation(string aniName)
    {
        //myAnimator.CrossFadeInFixedTime(aniName, 0.1f);
        myAnimator.CrossFadeInFixedTime(aniName, GameEngine.coreData.characterStates[currentState].blendRate);
        Debug.Log("Start: " + aniName);
    }

    public int currentCommandState;
    public int currentCommandStep;

    public void GetCommandState(bool isStyle)
    {
        MoveList list = isStyle ? GameEngine.gameEngine.CurrentStyleList() : GameEngine.gameEngine.CurrentMoveList();

        currentCommandState = 0;
        for (int c = 0; c < list.commandStates.Count; c++)
        {
            CommandState s = list.commandStates[c];
            if (s.aerial == aerialFlag)
            {
                currentCommandState = c;
                return;
            }
        }
    }

    int[] cancelStepList = new int[2];
    float commandTimeoutTimer = 0;
    float commandTimeoutDuration = 60f;

    void UpdateInput()
    {
        leftStick = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (Input.GetButton("RB")) { targeting = true; }
        else { targeting = false; }

        if (Input.GetButtonDown("RB"))
        {
            target = TargetClosestEnemy();
        }

        if (Input.GetButtonUp("LB"))
        {
            if (overclocked)
            {
                ExitOverclocked();
            }
            else
            {
                if (specialMeter >= 50)
                {
                    overclocked = true;
                    hasArmor = true;
                }
            }
        }

        if (Input.GetButtonDown("LT")) 
        {
            Debug.Log("LTDETECTED");
            if (GameEngine.gameEngine.globalMovelistIndex == 1)
            {
                // set to whatever spear throw attack is
                StartState(34);
            }
        }

        Vector2 Dpad = new Vector2(Input.GetAxisRaw("DPadX"), Input.GetAxisRaw("DPadY"));

        if (Dpad.y != 0)
        {
            if (Dpad.y == 1)
            {
                GameEngine.gameEngine.globalStylelistIndex = 0;
            }
            if (Dpad.y == -1)
            {
                GameEngine.gameEngine.globalStylelistIndex = 2;
            }
        }

        if (Dpad.x != 0)
        {
            if (Dpad.x == 1)
            {
                GameEngine.gameEngine.globalStylelistIndex = 1;
            }
            if (Dpad.x == -1)
            {
                GameEngine.gameEngine.globalStylelistIndex = 3;
            }
        }


        inputBuffer.Update();

        bool startState = false;

        bool isStyle = Input.GetButtonDown("Utility") || Input.GetButtonUp("Utility") || Input.GetButton("Utility");

        GetCommandState(isStyle);
        CommandState comState;

        if (isStyle)
        {
            comState = GameEngine.gameEngine.CurrentStyleList().commandStates[currentCommandState];
        }
        else
        {
            comState = GameEngine.gameEngine.CurrentMoveList().commandStates[currentCommandState];
        }

        // CommandState comState = GameEngine.gameEngine.CurrentMoveList().commandStates[currentCommandState];

        commandTimeoutTimer += Time.deltaTime * 60;

        if (currentCommandStep >= comState.commandSteps.Count) { currentCommandStep = 0; } // Change this to state specific or even commandstep specific variables
        if (commandTimeoutTimer >= commandTimeoutDuration) { currentCommandStep = 0; commandTimeoutTimer = 0; }

        cancelStepList[0] = currentCommandStep;
        cancelStepList[1] = 0;
        int finalS = -1;
        int finalF = -1;
        int currentPriority = -1;

        for (int s = 0; s < cancelStepList.Length; s++)
        {
            if (comState.commandSteps[currentCommandStep].strict && s > 0) { break; }
            if (!comState.commandSteps[currentCommandStep].activated) { break; } // what this do

            for (int f = 0; f < comState.commandSteps[cancelStepList[s]].followUps.Count; f++)
            {
                CommandStep nextStep = comState.commandSteps[comState.commandSteps[cancelStepList[s]].followUps[f]];
                InputCommand nextCommand = nextStep.command;

                // NEW priority stuff not implemented
                if (CheckInputCommand(nextCommand))
                {
                    if (canCancel)
                    {
                        if (GameEngine.coreData.characterStates[nextCommand.state].ConditionsMet(this))
                        {
                            if (nextStep.priority > currentPriority)
                            {
                                currentPriority = nextStep.priority;
                                startState = true;
                                finalS = s;
                                finalF = f;
                            }
                        }
                    }
                }
            }

        }

        if (startState)
        {
            commandTimeoutTimer = 0f;

            CommandStep nextStep = comState.commandSteps[comState.commandSteps[cancelStepList[finalS]].followUps[finalF]];
            InputCommand nextCommand = nextStep.command;
            inputBuffer.UseInput(nextCommand.input);
            if (nextStep.followUps.Count > 0) { currentCommandStep = nextStep.idIndex; }
            else { currentCommandStep = 0; }
            StartState(nextCommand.state);
        }
    }

    public bool CheckInputCommand(InputCommand _in)
    {       
        // new
        return inputBuffer.CheckCommand(_in);
        // OLD
        if (_in.inputType == InputCommand.InputCommandType.Motion)
        {
            if (inputBuffer.buttonCommandCheck[_in.input] < 0) { return false; }
            if (inputBuffer.motionCommandCheck[_in.motionCommand] < 0) { return false; }
        }
        return true;
    }

    public void SetVelocity(Vector3 v)
    {
        velocity = v;
    }


    LayerMask enemyLayerMask;
    public GameObject TargetClosestEnemy()
    {
        // HARDCODED LAYERMASK HERE BAD AAH 128 = 7 = Enemy
        Collider[] hitEnemies = Physics.OverlapSphere(character.transform.position + character.transform.forward, 20f, enemyLayerMask);
        GameObject closestEnemy = null;
        float shortestDist = Mathf.Infinity;

        GameObject _target = null;

        foreach (Collider hitEnemy in hitEnemies)
        {

            float dist = Vector3.Distance(character.transform.position, hitEnemy.transform.position);
            if (dist < shortestDist)
            {
                shortestDist = dist;
                closestEnemy = hitEnemy.gameObject;
            }
        }

        if (closestEnemy)
        {
            _target = closestEnemy;
        }
        else
        {
            _target = null;
        }


        return _target;
    }


    public Vector2 currHitAni;
    public Vector2 targetHitAni;
    public void GetHit(CharacterObject attacker)
    {
        if (invulnerable)
        {
            Time.timeScale = 0.15f;
            attacker.localTimescale = 0.15f;
            return;
        }

        Attack curAtk = GameEngine.coreData.characterStates[attacker.currentState].attacks[attacker.currentAttackIndex];

        // should be getter/setter instead of raw reset
        // does not take into acount multiple enemies rn
        attacker.hitActive = 0;
        curAtk.hitActive = 0;
        //Vector3 targetOffset = transform.position

        if (currentState == 21) // HARDCODED 21 = parry
        {
            Vector3 knockOrientation2 = -attacker.character.transform.forward;
            attacker.SetVelocity(Quaternion.LookRotation(knockOrientation2) * curAtk.knockback);
            GameEngine.SetHitStop(curAtk.hitStop);
            attacker.hitStun = 20;
            attacker.StartState(3); // 3 = hitstun state
            return;
        }

        hp--;
        Debug.Log(hp);

        // Hit animation
        currHitAni.x = curAtk.hitAni.x;
        currHitAni.y = curAtk.hitAni.y;

        // can += for little variants
        // put variance on the one that is lower as an idea (e.g. if attack is primarily up and down keep it the same so main motion isnt lost, so lr variant)
        targetHitAni.x += Random.Range(-0.1f, 0.1f);
        targetHitAni.y += Random.Range(-0.1f, 0.1f);

        currHitAni = targetHitAni * 0.7f; // multipy by target start position for hurt animation blending

        if (!HandleArmor(curAtk, attacker)) return;


        // Knockback
        Vector3 nextKnockback = curAtk.knockback;
        Vector3 knockOrientation = attacker.character.transform.forward;

        nextKnockback = Quaternion.LookRotation(knockOrientation) * nextKnockback;
        SetVelocity(nextKnockback);

        // hit reaction
        FaceTarget(attacker.transform.position);
        GameEngine.SetHitStop(curAtk.hitStop);

        hitStun = curAtk.hitstun;
        attacker.hitConfirm += 1;
        // attacker.BuildMeter(10f);

        if (curAtk.playerBoost != 0)
        {
            attacker.VelocityY(curAtk.playerBoost);
        }

        // Status Effects
        if (curAtk.statusData)
        {
            ApplyEffect(curAtk.statusData);
        }

        CheckIfDead(nextKnockback);

        // uneeded sets in startstate currentState = 3; 
        StartState(3); // magic number  hitstun state in coredata
        GlobalPrefab(0); // more magic number
    }

    private void CheckIfDead(Vector3 _knockback)
    {
        if (hp <= 0)
        {
            if (controlType == ControlType.AI)
            {
                hitStun = 60;
                _knockback = Vector3.Scale(_knockback, new Vector3(2f, 3f, 2f));
                _knockback.y = 1.5f;
                SetVelocity(_knockback);
            }
        }
    }

    private bool HandleArmor(Attack curAtk, CharacterObject attacker)
    {
        if (hasArmor && !isArmorBroken)
        {
            armorHealth -= 34;// curAtk.damage;

            if (armorHealth <= 0)
            {
                isArmorBroken = true;
                // Trigger armor break visual/sound/effect?
            }
            else
            {
                // Play stagger visual/audio without stun
                GameEngine.SetHitStop(curAtk.hitStop);
                // FaceTarget(attacker.transform.position);
                return false; // Cancel hit logic
            }
        }

        return true; // no armor, continue hit logic
    }
    public void GetShot(CharacterObject attacker)
    {
        if (invulnerable)
        {
            Time.timeScale = 0.15f;
            attacker.localTimescale = 0.15f;
            return;
        }

        hp--;
        Debug.Log(hp);
        VelocityY(0f);

        // can += for little variants
        // put variance on the one that is lower as an idea (e.g. if attack is primarily up and down keep it the same so main motion isnt lost, so lr variant)
        targetHitAni.x += Random.Range(-0.1f, 0.1f);
        targetHitAni.y += Random.Range(-0.1f, 0.1f);

        currHitAni = targetHitAni * 0.7f; // multipy by target start position for hurt animation blending

        FaceTarget(attacker.transform.position);

        hitStun = 30;
        noGrav = 23;

        CheckIfDead(new Vector3(0f, 0f, 0f));

        // uneeded sets in startstate currentState = 3; 
        StartState(3); // magic number  hitstun state in coredata
        GlobalPrefab(0); // more magic number
    }

    public float hitStun;
    public float noGrav;

    public bool isWallSlumped = false;
    LayerMask wallLayerMask;
    public void GettingHit()
    {
        hitStun -= Time.deltaTime * 60 * localTimescale;

        if (hitStun <= 0) { EndState(); }

        // Wall Slump
        // Add isKnockback flag to attacks that launch?
        // have a isInLaunchState flag or state? could evaluate if touching ground as well?
        // wall slump vs wall bounce

        if (!isWallSlumped)
        {
            Vector3 dir = velocity.normalized;
            Vector3 origin = transform.position + Vector3.up * 0.5f;

            // could use OnControllerColliderHit()?
            if (Physics.Raycast(origin, dir, out RaycastHit hit, 1.5f, wallLayerMask))
            {
                isWallSlumped = true;
                velocity.x *= -1;
                velocity.z *= -1;
                // StartState(CoreData.WALL_SLUMP_STATE); // Transition to slump state
                return;
            }
        }


        // HACKY PLACE FOR PROTOTYPE MOVE THIS
        //Dramatic freeze before death
        if (hitStun < 30 && hp <=0)
        {
            SetVelocity(new Vector3(0f, 0f, 0f));
        }

        // bending the current hit ani num to the target multiplied by blend rate
        // could change this blend based on hitstun
        currHitAni += (targetHitAni - currHitAni) * 0.25f;
    }

    bool IsVisibleOnScreen(GameObject enemy)
    {
        Vector3 screenPoint = Camera.main.WorldToViewportPoint(enemy.transform.position);

        // Check if the enemy is within the screen bounds
        bool isOnScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;

        if (isOnScreen)
        {
            /*
            // Optional: Ensure no obstacles block the view
            Ray ray = mainCamera.transform.position;
            if (Physics.Raycast(ray, out RaycastHit hit, detectionRadius))
            {
                return hit.collider.gameObject == enemy; // Only return true if the ray hits the enemy
            }
            */
            return true;
        }

        return false;
    }

    private GameObject _effectIcon;
    public void ApplyEffect(StatusEffectData _data)
    {
        RemoveEffect();
        this._data = _data;
        _effectIcon = Instantiate(_data.EffectIcon, transform);

    }

    public void RemoveEffect()
    {
        _data = null;
        currentEffectTime = 0;
        if (_effectIcon != null) { Destroy(_effectIcon); }
    }

    private float currentEffectTime = 0f;
    private float lastTickTime = 0f;
    public void HandleEffect()
    {
        currentEffectTime += 1; //or delta later

        if (currentEffectTime >= _data.Lifetime) RemoveEffect();
    }

    public void Collect(float _amnt)
    {
        BuildMeter(_amnt);
    }

    public void MinigameFail()
    {
//Camera.main.enabled = true;
        minigameCam.enabled = false;
        minigameCam.gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public void MinigameSuccess()
    {
        //Camera.main.enabled = true;
        minigameCam.enabled = false;
        minigameCam.gameObject.SetActive(false);
        hp = 5;
        Time.timeScale = 1;

        // get off me attack
    }
    public void WallBounce(Vector3 hitPosition)
    {
        Vector3 direction = character.transform.position - hitPosition;
        direction.y = 0;
        direction = direction.normalized;

        SetVelocity(new Vector3(direction.x * 0.6f, 0.6f, direction.z * 0.6f));
    }
}
