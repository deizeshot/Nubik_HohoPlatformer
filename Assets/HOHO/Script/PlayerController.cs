using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int playerID = 1;

    public float moveSpeed = 5;
    public float gravity = -35f;
    public float jumpHeightMax = 2;
    [Header("---SLIDING---")]
    public float slidingTime = 1;
    public float slidingCapsultHeight = 0.8f;
    float originalCharHeight, originalCharCenterY;

    [Header("---SETUP LAYERMASK---")]
    public LayerMask layerAsGround;
    public LayerMask layerAsWall;
    public LayerMask layerCheckHitHead;

    [Header("---WALL SLIDE---")]
    public float wallSlidingSpeed = 0.5f;
    [Tooltip("Player only can stick on the wall a little bit, then fall")]
    public float wallStickTime = 0.25f;
    [ReadOnly] public float wallStickTimeCounter;
    public Vector2 wallSlidingJumpForce = new Vector2(6,3);
    [ReadOnly] public bool isWallSliding = false;

    public CharacterController characterController { get; set; }
    [ReadOnly] public Vector2 velocity;
   [ReadOnly] public  float horizontalInput = 1;
    [ReadOnly] public bool isGrounded = false;
    Animator anim;
    bool isPlaying = false;
    [ReadOnly] public bool isSliding = false;
    [ReadOnly] public bool isDead = false;

    float velocityXSmoothing;
    public float accelerationTimeAirborne = .2f;
    public float accelerationTimeGroundedRun = .3f;
    public float accelerationTimeGroundedSliding = 1f;

    [Header("---AUDIO---")]
    public AudioClip soundFootStep;
    [Range(0f, 1f)]
    public float soundFootStepVolume = 0.5f;
    public AudioClip soundJump, soundHit, soundDie, soundSlide;

    public bool isInJumpZone { get; set; }

    void Start()
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, 0);
        characterController = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        transform.forward = new Vector3(horizontalInput, 0, 0);
        originalCharHeight = characterController.height;
        originalCharCenterY = characterController.center.y;
        jetpackObj.SetActive(false);
        SetCheckPoint(transform.position);

        jetpackAScr = jetpackObj.AddComponent<AudioSource>();
        jetpackAScr.clip = jetpackSound;
        jetpackAScr.volume = 0;
        jetpackAScr.loop = true;

        ropeRenderer = GetComponent<LineRenderer>();
    }

    void SetCheckPoint(Vector3 pos)
    {
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out hit, 100, layerAsGround))
        {
            GameManager.Instance.SetCheckPoint(hit.point);
        }
    }

    public void Play()
    {
        isPlaying = true;
    }

    void Update()
    {
        if (isGrabingRope)
        {
            transform.RotateAround(currentAvailableRope.transform.position, rotateAxis, horizontalInput * speed * Time.deltaTime);


            transform.up = currentAvailableRope.transform.position - transform.position;
            transform.Rotate(0, horizontalInput > 0 ? 90 : -90, 0);

            ropeRenderer.SetPosition(0, transform.position + transform.forward * grabOffset.x + transform.up * grabOffset.y);
            ropeRenderer.SetPosition(1, currentAvailableRope.transform.position);

            if (transform.position.y >= releasePointY)
            {
                if((horizontalInput>0 && transform.position.x > currentAvailableRope.transform.position.x) || (horizontalInput<0 && transform.position.x < currentAvailableRope.transform.position.x))
                //GrabRelease();      //disconnect grab if player reach to the limit position
                Flip();
            }
        }
        else if (climbingState != ClimbingState.ClimbingLedge)      //stop move when climbing
        {
            transform.forward = new Vector3(horizontalInput, 0, 0);

            float targetVelocityX = moveSpeed * horizontalInput;
            if (isSliding || isWallSliding)
                targetVelocityX = 0;

            velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref velocityXSmoothing, isGrounded ? (isSliding == false ? accelerationTimeGroundedRun : accelerationTimeGroundedSliding) : accelerationTimeAirborne);

            CheckGround();

            if (isGrounded && groundHit.collider.gameObject.tag == "Deadzone")
                HitAndDie();

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = 0;
                lastJumpZoneObj = null;
                CheckEnemy();
                CheckSpring();
                lastRopePointObj = null;
            }
            else if (isWallSliding)
            {
                velocity.y = -wallSlidingSpeed;
            }
            else
                velocity.y += gravity * Time.deltaTime;     //add gravity

            if (!isPlaying || isDead || isFallingAndDie)
                velocity.x = 0;

            if (!isUsingJetpack)
                CheckWallSliding();

            if (!isSliding && !isWallSliding)
                CheckHitHead();

            CheckEnemyAHead();

            Vector2 finalVelocity = velocity;
            if (isGrounded && groundHit.normal != Vector3.up)        //calulating new speed on slope
                GetSlopeVelocity(ref finalVelocity);

            //if (teleportTo)
            //{
            //    characterController.enabled = false;
            //    transform.position = teleportPos;
            //    characterController.enabled = true;
            //    teleportTo = false;
            //}else
            CheckLimitPos(ref finalVelocity);
            characterController.Move(finalVelocity * Time.deltaTime);

           
            if (isUsingJetpack)
                UpdateJetPackStatus();
        }

        CheckInput();
        HandleAnimation();

        if (isGrounded)
        {
            isInJumpZone = false;
            wallStickTimeCounter = wallStickTime;       //set reset wall stick timer when on ground
            CheckStandOnEvent();
        }

        ropeRenderer.enabled = isGrabingRope;
        CheckRopeInZone();      //only checking rope when in jump status

        if (!isUsingJetpack)
        {
            if (climbingState == ClimbingState.None && isGrounded)
                CheckLowerLedge();

            if (climbingState == ClimbingState.None && !isGrounded && velocity.y < 0)
                CheckLedge();
        }
    }

    void CheckLimitPos(ref Vector2 vel)
    {
        if (GameManager.Instance.gameState != GameManager.GameState.Playing)
            return;

        if (transform.position.y < Camera.main.ScreenToWorldPoint(new Vector3(0, 0, 10)).y)     //if player fall below the camera, player die
            Die();

        if (vel.y > 0 && (transform.position.y + characterController.height)
            >= Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, Camera.main.transform.position.z * -1)).y)
        {
            vel.y = 0;     //if player go higher than the camera, stop player
            velocity.y = 0;
        }
    }
    private void LateUpdate()
    {
        var finalPos = new Vector3(transform.position.x, transform.position.y, 0);
        transform.position = finalPos;    //keep the player stay 0 on Z axis
    }

    //bool teleportTo = false;
    //Vector3 teleportPos;
    public void TeleportTo(Vector3 pos)
    {
        //teleportTo = true;
        //teleportPos = pos;
        characterController.enabled = false;
        transform.position = pos;
        characterController.enabled = true;
        //teleportTo = false;
    }

    private void CheckStandOnEvent()
    {
        var hasEvent = (IPlayerStandOn)groundHit.collider.gameObject.GetComponent(typeof(IPlayerStandOn));
        if (hasEvent != null)
            hasEvent.OnPlayerStandOn();
    }

    void CheckEnemy()
    {
        var isEnemy = groundHit.collider.GetComponent<SimpleEnemy>();
        if (isEnemy)
        {
            if (isEnemy.canBeKillWhenPlayerJumpOn)
            {
                isEnemy.Kill();
                Jump(0.5f);
            }
            else
                HitAndDie();
        }
    }

    void CheckEnemyAHead()
    {
        RaycastHit hit;
        if (Physics.SphereCast(transform.position + Vector3.up * characterController.height * 0.5f,
            characterController.radius,
            horizontalInput > 0 ? Vector3.right : Vector3.left,
            out hit, 0.1f, 1<<LayerMask.NameToLayer("Enemy")))
        {
            HitAndDie();
        }
    }

    void CheckSpring()
    {
        var isSpring = groundHit.collider.GetComponent<TheSpring>();
        if (isSpring)
        {
            isSpring.Action();
            Jump(isSpring.pushHeight);
        }
    }

    void Flip()
    {
        horizontalInput *= -1;
    }

    void GetSlopeVelocity(ref Vector2 vel)
    {
        var crossSlope = Vector3.Cross(groundHit.normal, Vector3.forward);
        vel = vel.x * crossSlope;

        Debug.DrawRay(transform.position, crossSlope * 10);
    }

    void CheckWallSliding()
    {
        isWallSliding = false;
        if (isWallAHead())
        {
            velocity.x = 0;
            if (isFallingAndDie)
                return;     //stop checking if player in falling state

            if (!isGrounded && !isDead)
            {
                isWallSliding = true;
                wallStickTimeCounter -= Time.deltaTime;
                if (wallStickTimeCounter < 0)
                {
                    isWallSliding = false;
                    StartCoroutine(FallThenDieCo());        //active fall and die if player don't jump when sliding on wall
                }
            }
            else
                HitAndDie();
        }
    }

    bool isFallingAndDie = false;
    IEnumerator FallThenDieCo()
    {
        isFallingAndDie = true;
        while(!isGrounded) { yield return null; }
        HitAndDie();
    }

    bool isWallAHead()
    {
        RaycastHit hit;
        //if (Physics.CapsuleCast(transform.position + Vector3.up * characterController.height * 0.5f, transform.position + Vector3.up * (characterController.height - characterController.radius*2),
        //    characterController.radius, horizontalInput > 0 ? Vector3.right : Vector3.left, 0.1f, layerAsWall))
        if (Physics.SphereCast(transform.position + Vector3.up * characterController.height * 0.5f,
            characterController.radius,
            horizontalInput > 0 ? Vector3.right : Vector3.left,
            out hit, 0.1f, layerAsWall))
        {
            return true;
        }
        else
            return false;
    }

    void CheckHitHead()
    {
        if (isDead)
            return;

        //RaycastHit hit;
        //if (Physics.SphereCast(checkHitHead.position, 0.01f, horizontalInput > 0 ? Vector3.right : Vector3.left, out hit, 0.1f, layerCheckHitHead))
        //{
        //    Die();
        //}
        RaycastHit hit;
        //if (Physics.CapsuleCast(transform.position + Vector3.up * characterController.height * 0.5f, transform.position + Vector3.up * characterController.height,
        //    characterController.radius,  horizontalInput > 0 ? Vector3.right : Vector3.left, out hit, 0.1f, layerCheckHitHead))
        if (Physics.SphereCast(transform.position + Vector3.up * (characterController.height - characterController.radius),
           characterController.radius,
           horizontalInput > 0 ? Vector3.right : Vector3.left,
           out hit, 0.1f, layerCheckHitHead))
        {
            if (hit.point.y > characterController.center.y)
                HitAndDie();
        }
    }

    public void Victory()
    {
        isPlaying = false;
        GameManager.Instance.FinishGame();
        anim.SetBool("victory", true);
    }

    void HitAndDie()
    {
        if (isDead)
            return;

        SoundManager.PlaySfx(soundHit);
        Die();
    }

    public void Die()
    {
        if (isDead)
            return;

        SoundManager.PlaySfx(soundDie);
        isDead = true;
        velocity.x = 0;
        anim.applyRootMotion = true;
        if (isUsingJetpack)
            ActiveJetpack(false);

        GameManager.Instance.GameOver();
    }

    void HandleAnimation()
    {
        anim.SetFloat("speed", Mathf.Abs(velocity.x));
        anim.SetBool("isGrounded", isGrounded);
        anim.SetFloat("height speed", velocity.y);
        anim.SetBool("isSliding", isSliding);
        anim.SetBool("isWallSliding", isWallSliding);
        anim.SetBool("isDead", isDead);
        anim.SetBool("isFallingAndDie", isFallingAndDie);
        anim.SetBool("isUsingJetpack", isUsingJetpack);
        anim.SetBool("isGrabingRope", isGrabingRope);
    }

    void CheckInput()
    {
        if (currentAvailableRope != null)
        {
            if (Input.GetMouseButtonDown(0))
                GrabRope();

            if (Input.GetMouseButtonUp(0))
                GrabRelease();
        }
        else if (isUsingJetpack)
        {
            if (Input.anyKey)
            {
                velocity.y += jetForce * Time.deltaTime;
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space))
                Jump();

            if (Input.GetKeyDown(KeyCode.S))
                SlideOn();
        }
    }

    public void SlideOn()
    {
        if (climbingState == ClimbingState.ClimbingLedge)      //stop move when climbing
            return;

        if (GameManager.Instance.gameState != GameManager.GameState.Playing)
            return;

        if (!isGrounded)
            return;

        if (isSliding)
            return;

        if (isUsingJetpack)
            return;

        SoundManager.PlaySfx(soundSlide);
        isSliding = true;
        characterController.height = slidingCapsultHeight;
        var _center = characterController.center;
        _center.y = slidingCapsultHeight * 0.5f;
        characterController.center = _center;

        Invoke("SlideOff", slidingTime);
    }

    void SlideOff()
    {
        if (!isSliding)
            return;

        if (isUsingJetpack)
            return;

        if (climbingState == ClimbingState.ClimbingLedge)      //stop move when climbing
            return;

        characterController.height = originalCharHeight;
        var _center = characterController.center;
        _center.y = originalCharCenterY;
        characterController.center = _center;

        isSliding = false;
    }

    RaycastHit groundHit;
    void CheckGround()
    {
        isGrounded = false;
        if (velocity.y > 0.1f)
            return;

        if (Physics.SphereCast(transform.position + Vector3.up * 1, characterController.radius * 0.9f, Vector3.down, out groundHit, 1f, layerAsGround))
        {
            float distance = transform.position.y - groundHit.point.y;
            if (distance <= (characterController.skinWidth + 0.01f))
                isGrounded = true;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        Gizmos.DrawWireSphere(transform.position + Vector3.up * characterController.height * 0.5f, ropeCheckRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if (isGrounded)
        {
            Gizmos.DrawWireSphere(groundHit.point, characterController.radius * 0.9f);
        }
    }

    public void Jump(float newForce = -1)
    {
        if (isUsingJetpack)
            return;

        if (climbingState == ClimbingState.ClimbingLedge)      //stop move when climbing
            return;

        if (GameManager.Instance.gameState != GameManager.GameState.Playing)
            return;

        wallStickTimeCounter = wallStickTime;

        if (isWallSliding)
        {
            if (newForce == -1)
                SoundManager.PlaySfx(soundJump);

            isWallSliding = false;
            Flip();

            velocity.x = horizontalInput * Mathf.Abs(wallSlidingJumpForce.x);
            velocity.y += Mathf.Sqrt(wallSlidingJumpForce.y * -2 * gravity);
        }
        else if (isGrounded)
        {
            if (newForce == -1)
                SoundManager.PlaySfx(soundJump);

            if (isSliding)
                SlideOff();

            isGrounded = false;
            var _height = newForce != -1 ? newForce : jumpHeightMax;
            velocity.y += Mathf.Sqrt(_height * -2 * gravity);
            velocity.x = characterController.velocity.x;

            characterController.Move(velocity*Time.deltaTime);

        }else if (isInJumpZone)
        {
            if (newForce == -1)
                SoundManager.PlaySfx(soundJump);

            var _height = jumpHeightMax;
            velocity.y = Mathf.Sqrt(_height * -2 * gravity);
            velocity.x = characterController.velocity.x;

            characterController.Move(velocity * Time.deltaTime);
            isInJumpZone = false;
            Time.timeScale = 1;
        }
    }

    JumpZoneObj lastJumpZoneObj;
    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        var isTrigger = other.GetComponent<TriggerEvent>();
        if (isTrigger)
        {
            isTrigger.OnContactPlayer();
        }

        if (other.gameObject.tag == "Finish")
            Victory();
        else if (other.gameObject.tag == "Deadzone")
            Die();

        if (other.gameObject.tag == "Checkpoint")
            SetCheckPoint(other.transform.position);

        if (other.gameObject.tag == "TurnAround")
            Flip();

        if (other.gameObject.tag == "StopJetpack")
            ActiveJetpack(false);

        //if (other.gameObject.tag == "JumpZone")
        //    isInJumpZone = true;
        //Check Jump Zone
        var isJumpZone = other.GetComponent<JumpZoneObj>();
        if (lastJumpZoneObj != isJumpZone && isJumpZone != null)
        {
            isInJumpZone = true;
            isJumpZone.SetState(true);
           
            if (isJumpZone.slowMotion)
            {
                Time.timeScale = 0.1f;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //Check Jump Zone
        var isJumpZone = other.GetComponent<JumpZoneObj>();
        if (isJumpZone != null)
        {
            isInJumpZone = false;
            isJumpZone.SetState(false);
            if (velocity.y > 0)
                isJumpZone.SetStateJump();
            lastJumpZoneObj = isJumpZone;
            Time.timeScale = 1f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Deadzone")
        {
            HitAndDie();
        }
    }

    //Call by walk animation
    public void FootStep()
    {
        SoundManager.PlaySfx(soundFootStep, soundFootStepVolume);
    }

    #region LEDGE
    public enum ClimbingState { None, ClimbingLedge }
    public LayerMask layersCanGrab;
    [Header("------CLIBMING LEDGE------")]
    [ReadOnly] public ClimbingState climbingState;
    [Tooltip("Ofsset from ledge to set character position")]
    public Vector3 climbOffsetPos = new Vector3(0, 1.3f, 0);
    [Tooltip("Adjust to fit with the climbing animation length")]
    public float climbingLedgeTime = 1;
    public Transform verticalChecker;
    public float verticalCheckDistance = 0.5f;

    [Header("---CHECK LOW CLIMB 1m---")]
    [Tooltip("Ofsset from ledge to set character position")]
    public Vector3 climbLCOffsetPos = new Vector3(0, 1f, 0);
    public float climbingLBObjTime = 1;

   
    Transform ledgeTarget;      //use to update ledge moving/rotating
    Vector3 ledgePoint;

    bool CheckLowerLedge()      //check lower ledge
    {
        RaycastHit hitVertical;
        RaycastHit hitGround;
        RaycastHit hitHorizontal;

        //Debug.DrawRay(verticalChecker.position, Vector3.down * verticalCheckDistance);
        if (Physics.Linecast(verticalChecker.position, new Vector3(verticalChecker.position.x,transform.position.y + characterController.stepOffset,transform.position.z), out hitVertical, layersCanGrab, QueryTriggerInteraction.Ignore))
        {
            if(hitVertical.normal == Vector3.up) {
                //Debug.DrawRay(new Vector3(transform.position.x, hitVertical.point.y - 0.1f, verticalChecker.position.z), (horizontalInput > 0 ? Vector3.right : Vector3.left) * 2);
                if (Physics.Raycast(new Vector3(transform.position.x, hitVertical.point.y, verticalChecker.position.z), Vector3.down, out hitGround, 3, layersCanGrab, QueryTriggerInteraction.Ignore))
                {
                    if ((int)hitGround.distance <= 1)
                    {
                        if (Physics.Raycast(new Vector3(transform.position.x, hitVertical.point.y - 0.1f, verticalChecker.position.z), horizontalInput > 0 ? Vector3.right : Vector3.left, out hitHorizontal, 2, layersCanGrab, QueryTriggerInteraction.Ignore))
                        {
                            ledgeTarget = hitVertical.transform;
                            ledgePoint = new Vector3(hitHorizontal.point.x, hitVertical.point.y, transform.position.z);
                            velocity = Vector2.zero;
                            characterController.Move(velocity);
                            transform.position = CalculatePositionOnLedge(climbOffsetPos);
                            //reset other value
                            isWallSliding = false;
                            if (isSliding)
                                SlideOff();

                            StartCoroutine(ClimbingLedgeCo(true));
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    bool CheckLedge()       //check higher ledge
    {
        RaycastHit hitVertical;
        RaycastHit hitHorizontal;

        Debug.DrawRay(verticalChecker.position, Vector3.down * verticalCheckDistance);
        if (Physics.Raycast(verticalChecker.position, Vector2.down, out hitVertical, verticalCheckDistance, layersCanGrab, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawRay(new Vector3(transform.position.x, hitVertical.point.y - 0.1f, verticalChecker.position.z), (horizontalInput > 0 ? Vector3.right : Vector3.left) * 2);
            if (Physics.Raycast(new Vector3(transform.position.x, hitVertical.point.y - 0.1f, verticalChecker.position.z), horizontalInput>0 ? Vector3.right : Vector3.left, out hitHorizontal, 2, layersCanGrab, QueryTriggerInteraction.Ignore))
            {
                //climbingState = ClimbingState.ClimbingLedge;
                //allowClimbing = false;
                //anim.SetBool("ledgeHanging", true);
                //Invoke("AllowClimbing", 0.2f);
                ledgeTarget = hitVertical.transform;
                ledgePoint = new Vector3(hitHorizontal.point.x, hitVertical.point.y, transform.position.z);
                velocity = Vector2.zero;
                characterController.Move(velocity);
                transform.position = CalculatePositionOnLedge(climbOffsetPos);
                //reset other value
                isWallSliding = false;
                if (isSliding)
                    SlideOff();

                StartCoroutine(ClimbingLedgeCo(false));
                return true;
            }
        }
        return false;
    }

    private Vector3 CalculatePositionOnLedge(Vector3 offset)
    {
        Vector3 newPos = new Vector3(ledgePoint.x - (characterController.radius * (horizontalInput >0 ? 1 : -1)) - offset.x, ledgePoint.y - offset.y, transform.position.z);
     
        return newPos;
    }

    IEnumerator ClimbingLedgeCo(bool lowClimb)
    {
        climbingState = ClimbingState.ClimbingLedge;

        if (lowClimb)
            anim.SetBool("lowLedgeClimbing", true);
        else
            anim.SetBool("ledgeClimbing", true);

        HandleAnimation();
        yield return new WaitForSeconds(Time.deltaTime);
        characterController.enabled = false;
        anim.applyRootMotion = true;
        transform.position = CalculatePositionOnLedge(lowClimb? climbLCOffsetPos : climbOffsetPos);
        yield return new WaitForSeconds(Time.deltaTime);
        transform.position = CalculatePositionOnLedge(lowClimb ? climbLCOffsetPos : climbOffsetPos);

        yield return new WaitForSeconds(lowClimb ? climbingLBObjTime : climbingLedgeTime);
        LedgeReset();
    }

    void LedgeReset()
    {
        characterController.enabled = true;
        anim.applyRootMotion = false;
        climbingState = ClimbingState.None;
        anim.SetBool("ledgeClimbing", false);
        anim.SetBool("lowLedgeClimbing", false);
        ledgeTarget = null;
    }

    #endregion

    [Header("---JET PACK---")]
    public float jetForce = 5;
    public GameObject jetpackObj;
    public AudioClip jetpackSound;
    [Range(0f, 1f)]
    public float jetpackSoundVolume = 0.5f;
    AudioSource jetpackAScr;
    public ParticleSystem[] jetpackEmission;

    [ReadOnly]  public bool isUsingJetpack = false;

    public void ActiveJetpack(bool active)
    {
        if (active)
        {
            isUsingJetpack = true;
            jetpackObj.SetActive(true);
        }
        else
        {
            isUsingJetpack = false;
            jetpackObj.SetActive(false);
        }
    }

    void UpdateJetPackStatus()
    {
        for (int i = 0; i < jetpackEmission.Length; i++)
        {
            var emission = jetpackEmission[i].emission;
            emission.enabled = Input.anyKey;
            jetpackAScr.volume = (Input.anyKey & GlobalValue.isSound)? jetpackSoundVolume : 0;
        }
    }

    #region
    [Header("---ROPE--")]
    public Vector3 rotateAxis = Vector3.forward;
    public float speed = 100;
    public float releaseForce = 10;
    float distance, releasePointY;
    public float ropeCheckRadius = 6;
    [Tooltip("draw rope offset")]
    public Vector2 grabOffset = new Vector2(0, 1.6f);
    LineRenderer ropeRenderer;
    public AudioClip soundGrap, soundRopeJump;

    [ReadOnly] public bool isGrabingRope = false;
    [ReadOnly] public RopePoint currentAvailableRope;
    public LayerMask layerAsRope;
    RopePoint lastRopePointObj;

    void CheckRopeInZone()
    {
        if (isGrabingRope)
            return;

        var hits = Physics.OverlapSphere(transform.position + Vector3.up * characterController.height * 0.5f, ropeCheckRadius, layerAsRope);
        
        if (hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (horizontalInput > 0)
                {
                    if (hits[i].transform.position.x > transform.position.x)
                    {
                        currentAvailableRope = hits[i].GetComponent<RopePoint>();
                        if (lastRopePointObj != currentAvailableRope)
                        {
                            if (currentAvailableRope.slowMotion)
                                Time.timeScale = 0.1f;
                        }else
                            currentAvailableRope = null;
                    }
                    else
                        currentAvailableRope = null;
                }
                else
                {
                    if (hits[i].transform.position.x < transform.position.x)
                    {
                        currentAvailableRope = hits[i].GetComponent<RopePoint>();
                        if (lastRopePointObj != currentAvailableRope)
                        {
                            if (currentAvailableRope.slowMotion)
                            Time.timeScale = 0.1f;
                        }
                        else
                            currentAvailableRope = null;
                    }
                    else
                        currentAvailableRope = null;
                }
            }
        }
        else
        {
            if (currentAvailableRope != null)       //set time scale back to normal if it active the slow motion before but player don't grab
            {
                if (currentAvailableRope.slowMotion)
                    Time.timeScale = 1;
            }

            currentAvailableRope = null;
        }
    }

    public void GrabRope()
    {
        if (isGrabingRope)
            return;

        if (isGrounded)
            return;     //don't allow grab rope when standing on ground

        if (lastRopePointObj != currentAvailableRope)
        {
            if (currentAvailableRope.slowMotion)
                Time.timeScale = 1;

            lastRopePointObj = currentAvailableRope;
            isGrabingRope = true;
            SoundManager.PlaySfx(soundGrap);
            distance = Vector2.Distance(transform.position, currentAvailableRope.transform.position);
            releasePointY = currentAvailableRope.transform.position.y - distance / 10f;
        }
    }

    public void GrabRelease()
    {
        if (!isGrabingRope)
            return;

        velocity = releaseForce * transform.forward;
        characterController.Move(velocity * Time.deltaTime);
        Time.timeScale = 1;
        SoundManager.PlaySfx(soundRopeJump);
        isGrabingRope = false;
    }

    #endregion
}
