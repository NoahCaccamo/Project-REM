using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
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

    public int hp = 10;

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

    public bool wallrunning = false;

    public bool invulnerable = false;

    public bool faceStick = false;

    // experimental Ai stuff
    public NavMeshAgent _agent;
    public Transform _playerTrans;
    private Vector3 _desVelocity;
    private float _speed = 2;
    private float _turnSpeed = 3;

    public LayerMask whatIsPlayer;


    void Start()
    {
        myController = GetComponent<CharacterController>();
        // myAnimator = GetComponent<Animator>();
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
    }
    private void OnApplicationQuit()
    {
        inventory.Container.Clear();
    }
    // END INVENTORY STUFF

    private void Updaste()
    {
        // hitcancel preferabbly before input but does this need to be in fixed update
        // HitCancel();


        /*
        if (GameEngine.hitStop <= 0)
        {
            switch (controlType)
            {
                case ControlType.AI:
                    UpdateAI();
                    break;

                case ControlType.PLAYER:
                    UpdateInput();
                    break;
            }
        }
        */

    }

    public float atkCooldown = 60;
    void UpdateAI()
    {
        
        if (hitStun > 0)
        {
            atkCooldown = 60;
            return;
        }
        /* wack navmesh hacky stuff
        

        SetVelocity(new Vector3(0f,0f,0f));
        Vector3 lookPos;
        Quaternion targetRot;

        this._agent.destination = this._playerTrans.position;
        this._desVelocity = this._agent.desiredVelocity;

        this._agent.updatePosition = false;
        this._agent.updateRotation = false;

        lookPos = this._playerTrans.position - this.transform.position;
        lookPos.y = 0;
        targetRot = Quaternion.LookRotation(lookPos);
        this.transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * this._turnSpeed);

       SetVelocity(this._desVelocity.normalized * this._speed * Time.deltaTime);
        */


        bool playerInSightRange = Physics.CheckSphere(transform.position, 10, whatIsPlayer);
        bool playerInAttackRange = Physics.CheckSphere(transform.position, 2, whatIsPlayer);

        if (!playerInSightRange && !playerInAttackRange) { target = null; return; }
        if (playerInSightRange && !playerInAttackRange) ChasePlayer();
        if (playerInSightRange && playerInAttackRange) AttackPlayer();
    }

    private void ChasePlayer()
    {
        Vector3 velDir = new Vector3(0, 0, 0);

        velDir = this._playerTrans.position - this.gameObject.transform.position;
        velDir.y = 0;
        velDir.Normalize();

        velDir *= 0.02f;

        velocity += velDir * Time.deltaTime * 60;
    }

    private void AttackPlayer()
    {
        atkCooldown--;
        if (atkCooldown == 0)
        {
            FaceTarget(this._playerTrans.position);
            StartState(5); // Enemy punch
        }
        if (atkCooldown < -120)
        {
            atkCooldown = 60;
        }
    }


    void Update()
    {
        switch (controlType)
        {
            case ControlType.AI:
                UpdateAI();
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

    void UpdateTimers()
    {
        if (dashCooldown > 0) { dashCooldown -= dashCooldownRate * 60 * Time.deltaTime; }
    }

    public float aniMoveSpeed;
    public float aniSpeed;
    void UpdateAnimation()
    {
        aniSpeed = 1;

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

            velocity += velHelp * Time.deltaTime * 60;
        }
    }

    void FaceVelocity()
    {
        if (CheckVelocityDeadZone())
        {
            character.transform.rotation = Quaternion.LookRotation(new Vector3(velocity.x, 0, velocity.z), Vector3.up);
        }
    }

    void FaceTarget(Vector3 tarPos)
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

        velocity += gravity * Time.deltaTime * 60;//* localTimescale;

        myController.Move(velocity * 60 * Time.deltaTime);// * localTimescale);

        velocity.x = Mathf.Lerp(velocity.x, 0, friction.x * Time.deltaTime * 60);
        velocity.y = Mathf.Lerp(velocity.y, 0, friction.y * Time.deltaTime * 60);
        velocity.z = Mathf.Lerp(velocity.z, 0, friction.z * Time.deltaTime * 60);



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
                aerialTimer += Time.deltaTime * 60;
            }
            if (aerialTimer >= 3)
            {
                aerialFlag = true;
                if (aniAerialState <= 1f)
                {
                    aniAerialState += 0.1f * 60 *Time.deltaTime; // 0.05 is 20 frames i think since 0.1 is 10
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
        currentStateTime += 60 * Time.deltaTime;

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
        currentStateTime = 0;
        currentState = 0;
        prevStateTime = -1;
        StartState(currentState);

        // DESTROY AFTER HITSTUN IS DONE HACKY
        if (hp <= 0)
        {
            Destroy(this.gameObject);
        }
    }

    void UpdateStateEvents()
    {
        foreach(StateEvent _ev in GameEngine.coreData.characterStates[currentState].events)
        {

            // <= here might fire things twice on first frame?
            // theres a better way to do this
            //if (prevStateTime <= _ev.start && currentStateTime == _ev.start)
            if (!_ev.hasExecuted)
            {
                if (_ev.start == _ev.end && currentStateTime >= _ev.start)
                {
                    DoEventScript(_ev.script, _ev.variable);
                    _ev.hasExecuted = true;
                }
                if (currentStateTime >= _ev.start && currentStateTime <= _ev.end) // after the && is hacky
                {
                    DoEventScript(_ev.script, _ev.variable);
                }
            }
        }
    }

    public float hitActive;
    public int currentAttackIndex;

    void UpdateStateAttacks()
    {
        int _cur = 0;
        foreach (Attack _atk in GameEngine.coreData.characterStates[currentState].attacks)
        {
            // hitconfirm here with sepearate flag if we want to count attack hits rather than skill?
            if (currentStateTime >= _atk.start && prevStateTime < _atk.start) // could do a greater catch window when manipulating timescale
            {
                hitActive = _atk.length;
                hitbox.transform.localScale = _atk.hitboxScale;
                hitbox.transform.localPosition = _atk.hitboxPos;
                currentAttackIndex = _cur;
            }

            if (currentStateTime >= _atk.start + _atk.length)
            {
                hitActive = 0;
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

    void DoEventScript(int _index, float _var)
    {
        switch(_index)
        {
            case 0:
                VelocityY(_var);
                break;
            case 1:
                FrontVelocity(_var);
                break;
            case 3:
                CameraRelativeStickMove(_var);
                break;
            case 4:
                GettingHit();
                break;
            case 5:
                GlobalPrefab(_var);
                break;
            case 6:
                CanCancel(_var);
                break;
            case 7:
                Jump(_var);
                break;
            case 8:
                FaceVelocity();
                break;
            case 9:
                FaceStick(_var);
                break;
            case 10:
                VelocityToTarget(_var);
                break;
            case 11:
                Invulnerable(_var);
                break;
        }
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
            velocity += targetDir * _pow * Time.deltaTime * 60;

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
        velocity += character.transform.forward * _pow * Time.deltaTime * 60;
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

        faceStick = false;

        // Attack
        hitActive = 0;
        hitConfirm = 0;

        // Reset oneshot event flags
        foreach (StateEvent _ev in GameEngine.coreData.characterStates[currentState].events)
        {
            if (_ev.hasExecuted)
            {
                _ev.hasExecuted = false;
            }
        }

            SetAnimation(GameEngine.coreData.characterStates[currentState].stateName);

        if (hitStun <= 0) { FaceStick(1); faceStick = false; }

        if (softTarget)
        {
            FaceTarget(softTarget.gameObject.transform.position);
            faceStick = false;
        }
    }

    void SetAnimation(string aniName)
    {
        //myAnimator.CrossFadeInFixedTime(aniName, 0.1f);
        myAnimator.CrossFadeInFixedTime(aniName, GameEngine.coreData.characterStates[currentState].blendRate);
        Debug.Log("Start: " + aniName);
    }

    public int currentCommandState;
    public int currentCommandStep;

    public void GetCommandState()
    {
        currentCommandState = 0;
        for (int c = 0; c < GameEngine.coreData.commandStates.Count; c++)
        {
            CommandState s = GameEngine.coreData.commandStates[c];
            if (s.aerial == aerialFlag)
            {
                currentCommandState = c;
                return;
            }
        }
    }

    int[] cancelStepList = new int[2];

    void UpdateInput()
    {
        leftStick = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (Input.GetButton("RB"))
        {
            targeting = true;
            //localTimescale = 0.2f;
            Time.timeScale = 0.2f;
        }
        else { targeting = false; }

        if (Input.GetButtonDown("RB")) 
        {
            target = TargetClosestEnemy();
        }

        inputBuffer.Update();

        bool startState = false;

        GetCommandState();
        CommandState comState = GameEngine.coreData.commandStates[currentCommandState];

        if (currentCommandStep >= comState.commandSteps.Count) { currentCommandStep = 0; } // Change this to state specific or even commandstep specific variables

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
            CommandStep nextStep = comState.commandSteps[comState.commandSteps[cancelStepList[finalS]].followUps[finalF]];
            InputCommand nextCommand = nextStep.command;
            inputBuffer.UseInput(nextCommand.input);
            if (nextStep.followUps.Count > 0) { currentCommandStep = nextStep.idIndex; }
            else { currentCommandStep = 0; }
            StartState(nextCommand.state);
        }

                // OLD INPUT BUFFER STUFF

                /*
                if (startState) { break; }
                foreach (InputBufferItem bItem in inputBuffer.inputList)
                {
                    if (startState) { break; }
                    foreach (InputStateItem bState in bItem.buffer)
                    {
                        if (stepCommand.input == bItem.button)
                        {
                            if (bState.CanExecute())
                            {
                                if (canCancel)
                                {
                                    if (GameEngine.coreData.characterStates[stepCommand.state].ConditionsMet(this))
                                    {
                                        startState = true;
                                        bState.used = true;

                                        if (nextStep.followUps.Count > 0) { currentCommandStep = nextStep.idIndex; }
                                        else { currentCommandStep = 0; }
                                        Debug.Log("Current Step: " + currentCommandStep);
                                        StartState(stepCommand.state);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                
                }
                */

        /*
        foreach (InputCommand c in GameEngine.coreData.commands)
        {

        }
        */

            /*
            if (c.inputString != "")
            {
                if (Input.GetButtonDown(GameEngine.coreData.commands[c].inputString))
                {
                    if (canCancel)
                    {
                        print(GameEngine.coreData.commands[c].state);
                        StartState(GameEngine.coreData.commands[c].state);
                        break;
                    }
                    // Continue from here!
                    // --> Hold state until out of command list and then check if you can Cancel or not
                }
            }
            */

            //for (int c = 0; c < GameEngine.coreData.commands.Count; c++) // (InputCommand c in GameEngine.coreData.commands)
            //{
            //    if (GameEngine.coreData.commands[c].inputString != "")
            //    {
            //        // wack have to line up order here. bad. fix
            //        for (int b = 0; b < inputBuffer.inputList[c].buffer.Count; b++)
            //        {
            //            if (Input.GetButtonDown(GameEngine.coreData.commands[c].inputString))
            //            {
            //                if (canCancel)
            //                {
            //                    print(GameEngine.coreData.commands[c].state);
            //                    StartState(GameEngine.coreData.commands[c].state);
            //                    break;
            //                }
            //                // Continue from here!
            //                // --> Hold state until out of command list and then check if you can Cancel or not
            //            }
            //        }
            //    }
            //}
    }

    public bool CheckInputCommand(InputCommand _in)
    {
        if (inputBuffer.buttonCommandCheck[_in.input] < 0) { return false; }
        if (inputBuffer.motionCommandCheck[_in.motionCommand] < 0) { return false; }
        return true;
    }

    public void SetVelocity(Vector3 v)
    {
        velocity = v;
    }

    public GameObject TargetClosestEnemy()
    {
        // HARDCODED LAYERMASK HERE BAD AAH 128 = 7 = Enemy
        Collider[] hitEnemies = Physics.OverlapSphere(character.transform.position + character.transform.forward, 20f, 128);
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
            return;
        }

        Attack curAtk = GameEngine.coreData.characterStates[attacker.currentState].attacks[attacker.currentAttackIndex];

        // should be getter/setter instead of raw reset
        // does not take into acount multiple enemies rn
        attacker.hitActive = 0;
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

        Vector3 nextKnockback = curAtk.knockback;
        Vector3 knockOrientation = attacker.character.transform.forward;

        nextKnockback = Quaternion.LookRotation(knockOrientation) * nextKnockback;
        SetVelocity(nextKnockback);
        currHitAni.x = curAtk.hitAni.x;
        currHitAni.y = curAtk.hitAni.y;

        // can += for little variants
        // put variance on the one that is lower as an idea (e.g. if attack is primarily up and down keep it the same so main motion isnt lost, so lr variant)
        targetHitAni.x += Random.Range(-0.1f, 0.1f);
        targetHitAni.y += Random.Range(-0.1f, 0.1f);

        currHitAni = targetHitAni * 0.7f; // multipy by target start position for hurt animation blending

        FaceTarget(attacker.transform.position);
        GameEngine.SetHitStop(curAtk.hitStop);

        hitStun = curAtk.hitstun;
        attacker.hitConfirm += 1;
        attacker.BuildMeter(10f);

        if (curAtk.playerBoost != 0)
        {
            attacker.VelocityY(curAtk.playerBoost);
        }

        // Status Effects
        if (curAtk.statusData)
        {
            ApplyEffect(curAtk.statusData);
        }


        // hacky death check in wrong place and should be its own state
        if (hp <= 0)
        {
            hitStun = 60;
            nextKnockback = Vector3.Scale(nextKnockback, new Vector3(2f, 3f, 2f));
            nextKnockback.y = 1.5f;
            SetVelocity(nextKnockback);
        }

        // uneeded sets in startstate currentState = 3; 
        StartState(3); // magic number  hitstun state in coredata
        GlobalPrefab(0); // more magic number
    }

    public float hitStun;
    public void GettingHit()
    {
        hitStun -= Time.deltaTime * 60;
        if (hitStun <= 0) { EndState(); }

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
}
