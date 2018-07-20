using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLogic : MonoBehaviour {

    private const float LOOK_LIMIT_Y = 89f;

    // PUBLIC FIELDS
    // ================
    public Camera       playerCam;
    public GameObject   playerFeet;
    public GameObject   hookPrefab;
    public Texture2D    XhairTex;
    public float playerAccel;
    public float playerAirAccel;
    public float playerMaxVel;
    public float playerMaxAirVel;
    public float playerFriction;
    public float playerJumpVel;
    public float groundCheckRadius;
    public float groundCheckDistance;
    public float gravity;
    public float hookDistance;
    public float hookShootDistance;

    // PRIVATE FIELDS
    // ================
    private CapsuleCollider playerCollider;
    private Rigidbody   playerRB;
    private Quaternion  aimQuat;
    private Vector2     lookXY;
    private GameObject[] hookObj   = new GameObject[2];
    private Vector3[]   hookPoint  = new Vector3[2];
    private Vector3[]   hookVel    = new Vector3[2];
    private float[]     hookLength = new float[2];
    private bool[]      hooked     = new bool[2];
    private bool        isGrounded;
    private bool        canJump;
    private Vector3     groundNormal;
    private bool        mouseLocked;

    private bool    debugShowPlayerAttrib = false;
    private float[] debugList;
    private Vector3 debugWishDir;

    // CODE GOES HERE!!
    // =====================

    // PM_Accel
    private void Accelerate (Vector3 wishDir, float wishVel, float acceleration)
    {
        /*
         *  BHOP-GUIDE VERSION
            http://flafla2.github.io/2015/02/14/bunnyhop.html
        */
        /*
            float currentVel = Vector3.Dot( playerVel, wishDir );
            float accelVel = acceleration * Time.fixedDeltaTime;
            if (accelVel + currentVel > wishVel)
            {
                accelVel = wishVel - currentVel;
            }

            debugList[0] = currentVel;
            debugList[1] = accelVel;
        */

        /*
            SOURCE ENGINE VERSION
            (https://github.com/ValveSoftware/source-sdk-2013/blob/56accfdb9c4abd32ae1dc26b2e4cc87898cf4dc1/sp/src/game/shared/gamemovement.cpp)
        */
        
        float currentVel = Vector3.Dot( playerVel, wishDir );
        float addVel = wishVel - currentVel;

        debugList[0] = currentVel;

        if (addVel <= 0)
            return;

        // get amount of accel
        float accelVel = acceleration * Time.deltaTime;
        accelVel = Mathf.Min( accelVel, addVel ); // and cap it

        debugList[1] = accelVel;

        // add velocity
        playerVel += wishDir * accelVel;
    }

    // Keep players sticked to ground
    private void KeepGrounded ()
    {
        RaycastHit hitInfo;
        int levelMask = 1 << LayerMask.NameToLayer( "Level" );

        bool hitGround = Physics.SphereCast( playerFeet.transform.position, playerCollider.radius, Vector3.down, out hitInfo, playerCollider.radius * 4f, levelMask ) && !Physics.CheckSphere( playerFeet.transform.position, groundCheckRadius, levelMask );

        if (hitGround)
        {
            float heightDelta   = Mathf.Abs((playerFeet.transform.position.y - playerCollider.radius) - hitInfo.point.y);
            transform.position -= new Vector3( 0, heightDelta, 0 );

            // oh no got stucc
            if (Physics.CheckSphere( playerFeet.transform.position, groundCheckRadius, levelMask ))
            {
                heightDelta = (playerFeet.transform.position.y - playerCollider.radius) - hitInfo.point.y;
                transform.position -= new Vector3( 0, heightDelta, 0 );
            }
        }
    }

    // Move scripts
    private void GroundMove ( Vector3 wishDir )
    {
        // Jumping
        // ==================
        bool jumpInput = Input.GetButton( "Jump" );

        if (canJump && jumpInput && isGrounded)
        {
            playerVel = new Vector3( playerVel.x, playerJumpVel, playerVel.z );
            isGrounded = false;
            canJump = false;
        }

        // friction
        if (isGrounded)
        {
            float currentSpeed = playerVel.magnitude;
            if (currentSpeed != 0)
            {
                float drop = currentSpeed * playerFriction * Time.deltaTime;
                playerVel *= Mathf.Max( currentSpeed - drop ) / currentSpeed;
            }

            // Fuck this, Too glitch :/
            // KeepGrounded();
        }

        // fek u floating point error
        if (playerVel == Vector3.zero)
            playerVel = Vector3.zero;

        // Ground
        wishDir = Vector3.ProjectOnPlane( wishDir, groundNormal );

        Accelerate( wishDir, playerMaxVel, playerAccel );
    }

    private void AirMove ( Vector3 wishDir )
    {
        Accelerate( wishDir, playerMaxAirVel, playerAirAccel );
    }

    // handle Mouse Input
    private void UpdateMouseInput ()
    {
        // Lock the mouse accordingly to mouse lock state
        Cursor.lockState = CursorLockMode.Locked;// mouseLocked ? CursorLockMode.Locked : CursorLockMode.None;

        // Get mouse delta
        Vector2 mouseDelta = new Vector2( Input.GetAxis( "Mouse X" ), Input.GetAxis( "Mouse Y" ) );

        // Add to aim pitch n' yaw
        // Why make a variable JUST for storing mouse pitch / yaw instead of Using the mouseDelta directly?
        // Because juuuuust in case of Input code's expandability.
        lookXY += new Vector2( mouseDelta.x, -mouseDelta.y );

        // loop horizontal
        if (lookXY.x < 0)
            lookXY.x += 360;
        if (lookXY.x > 360)
            lookXY.x -= 360;

        // limit vertical
        lookXY.y = Mathf.Clamp( lookXY.y, -LOOK_LIMIT_Y, LOOK_LIMIT_Y );

        // OK, Now we can use lookXY to actually update our player aim thing.
        aimQuat = Quaternion.Euler( lookXY.y, lookXY.x, 0f );
        playerCam.transform.rotation = aimQuat;

        // Now we're shootin'
        bool[] shootInput = new bool[] { Input.GetMouseButtonDown( 0 ), Input.GetMouseButtonDown( 1 ) };

        if (shootInput[0])
            ShootHook( 0 );

        if (shootInput[1])
            ShootHook( 1 );
    }

    // Handle keyboard Input
    private void UpdateKeyboardInput ()
    {
        // Forward, Right
        Vector3 plForward = aimQuat * Vector3.forward;
        Vector3 plRight   = aimQuat * Vector3.right;

        // Get keyboard Input
        // Walking
        // ==================
        Vector2 moveInput = new Vector2( Input.GetAxisRaw( "Horizontal" ), Input.GetAxisRaw( "Vertical" ) );
        Vector3 wishDir = moveInput.y * plForward + moveInput.x * plRight;
        wishDir.y = 0;
        wishDir.Normalize();

        debugWishDir = wishDir;

        // accel accordingly to state
        if (isGrounded)
        {
            GroundMove( wishDir );
        }
        else
        {
            AirMove( wishDir );
        }

        // Mouse locking
        if ( Input.GetKeyDown( KeyCode.Escape ) )
        {
            mouseLocked = !mouseLocked;
        }
    }

    private void CheckGround ()
    {
        RaycastHit hitInfo;
        int levelMask = 1 << LayerMask.NameToLayer( "Level" );

        bool hitGround = Physics.SphereCast( playerFeet.transform.position, groundCheckRadius, Vector3.down, out hitInfo, groundCheckDistance, levelMask ) ||
                         Physics.CheckSphere( playerFeet.transform.position, groundCheckRadius, levelMask );
        Debug.DrawRay( playerFeet.transform.position, Vector3.down * groundCheckDistance );

        if (hitGround)
        {
            if (!isGrounded && playerVel.y <= 0)
                canJump = true;

            groundNormal = hitInfo.normal;

            if (Vector3.Angle( Vector3.up, groundNormal ) < 30f)
            {
                isGrounded = true;
                // Debug.Log("You're grounded");
            }
            else
            {
                isGrounded = false;
            }

            // Debug.Log( Vector3.Angle( Vector3.up, groundNormal ) );
        }
        else
        {
            groundNormal = Vector3.zero;
            isGrounded = false;
        }
    }

    // Schut mann
    private void ShootHook ( int hookIndex )
    {
        if (hookIndex >= 2 || hookIndex < 0)
        {
            Debug.Log( "[SHOOTHOOK] FUCK WRONG INDEX (" + hookIndex + ')' );
            return;
        }

        // TODO : Add shooting mechanic
        Vector3 aimForward = aimQuat * Vector3.forward;
        int grappleMask = (1 << LayerMask.NameToLayer( "Hookable" )) | (1 << LayerMask.NameToLayer( "Level" ));

        // Cast ray and check hit
        RaycastHit hitInfo;
  
        Debug.DrawRay( playerCam.transform.position, aimForward * hookShootDistance, Color.red, 2 );
        if (Physics.Raycast( playerCam.transform.position, aimForward, out hitInfo, hookShootDistance, grappleMask ))
        {
            Vector3 hitPos = hitInfo.point;
            //Debug.Log( "HIT" );
            hookPoint[hookIndex]    = hitPos;
            hooked[hookIndex]       = true;
            hookLength[hookIndex]   = Vector3.Distance( hitPos, transform.position );
            //Debug.Log( "SETVAR" );

            GameObject hook = hookObj[hookIndex];

            // no hook, make one
            if (hook == null || !hook.scene.IsValid())
            {
                hookObj[hookIndex] = GameObject.Instantiate( hookPrefab );
            }
            hookObj[hookIndex].transform.position = hitPos;
            hook = hookObj[hookIndex];

            // reset velocity
            hookVel[hookIndex] = Vector3.zero;

            // add joint
            SetHookJointAttrib( hookIndex, hookDistance, 5f, 0.5f );
        }
    }

    // Grapple
    private void UpdateHook ( int index )
    {
        if (index >= 2 || index < 0)
        {
            Debug.Log( "[UPDATEHOOK] FUCK WRONG INDEX (" + index + ')' );
            return;
        }

        // Check if the hook is hooked
        if ( !hooked[index] )
        {
            return; // nah no need to update
        }

        hookObj[index].transform.position = hookPoint[index];
        hookObj[index].GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition;
    }

    private void UpdateHookAll ()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!hooked[i])
                continue;

            // Fix hook position
            hookObj[i].transform.position = hookPoint[i];
            hookObj[i].GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition;
        }
    }

    private void SetHookJointAttrib (int index, float restDist, float spring, float damp)
    {
        if (index >= 2 || index < 0)
        {
            Debug.Log( "[SETHOOK] FUCK WRONG INDEX (" + index + ')' );
            return;
        }

        // Check if the hook is hooked
        if (!hooked[index])
        {
            return; // nah no need to update
        }

        GameObject hook = hookObj[index];

        // set joint
        // add joint if needed
        SpringJoint tempJoint = hook.GetComponent<SpringJoint>();
        if (tempJoint == null)
        {
            tempJoint = hook.AddComponent<SpringJoint>();
        }
        
        tempJoint.connectedBody = playerRB;
        tempJoint.minDistance = 0;
        tempJoint.maxDistance = restDist;
        tempJoint.spring      = spring;
        tempJoint.damper      = damp;
        tempJoint.axis        = Vector3.zero;

        tempJoint.enableCollision = false;
    }

    private void SetHookJointAttribAll (float restDist, float spring, float damp)
    {
        for (int i = 0; i < 2; i++)
        {
            if (!hooked[i])
                continue;

            GameObject hook = hookObj[i];

            // set joint
            // add joint if needed
            SpringJoint tempJoint = hook.GetComponent<SpringJoint>();
            if (tempJoint == null)
            {
                tempJoint = hook.AddComponent<SpringJoint>();
            }

            tempJoint.connectedBody = playerRB;
            tempJoint.minDistance = 0;
            tempJoint.maxDistance = restDist;
            tempJoint.spring = spring;
            tempJoint.damper = damp;
        }
    }

    private void OnGUI()
    {

        if (debugShowPlayerAttrib)
        {
            Rect bigRect = new Rect ( 0, 0, 200f, 100f );
            GUIStyle myStyle = new GUIStyle();
            myStyle.fontSize = 20;

            GUI.Label( bigRect, "PlayerVel : " + playerVel, myStyle );

            bigRect.y += 20f;
            GUI.Label( bigRect, "CurrentVel : " + debugList[0], myStyle );

            bigRect.y += 20f;
            GUI.Label( bigRect, "AccelVel : " + debugList[1], myStyle );

            bigRect.y += 20f;
            GUI.Label( bigRect, "WishDir : " + debugWishDir, myStyle );

            bigRect.y += 20f;
            Vector3 horVel = playerVel;
            horVel.y = 0;

            GUI.Label( bigRect, "Horizontal Speed : " + horVel.magnitude, myStyle );

            myStyle.fontSize = 25;
            myStyle.alignment = TextAnchor.MiddleCenter;

            bigRect = new Rect( 690, 360, 400, 400f );
            GUI.Label( bigRect, "GROUND : " + isGrounded, myStyle );

            bigRect = new Rect( 690, 400, 400, 400f );
            GUI.Label( bigRect, "MORMAL : " + groundNormal, myStyle );
        }

        float XhairSize = XhairTex.width * 2;
        GUI.DrawTexture( new Rect( Screen.width / 2 - XhairSize / 2, Screen.height / 2 - XhairSize / 2, XhairSize, XhairSize ), XhairTex );
    }

    private void OnDrawGizmos()
    {
        
    }

    // Use this for initialization
    void Start ()
    {
        debugList = new float[3];

        playerRB = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();
        lookXY = Vector2.zero;
        isGrounded = true;
    }
	
	// Update is called once per frame
	void Update ()
    {
        if (!isGrounded)
            playerVel -= new Vector3( 0, gravity, 0 );

        CheckGround();

        // Handle inputs
        UpdateMouseInput();
        UpdateKeyboardInput();

        // Handle hooks
        UpdateHookAll();
        //UpdateHook( 0 );
        //UpdateHook( 1 );
    }

    Vector3 playerVel
    {
        get { return playerRB.velocity; }
        set { playerRB.velocity = value; }
    }
}
