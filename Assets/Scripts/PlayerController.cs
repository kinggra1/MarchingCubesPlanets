using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {

    #region Static parameters defining movement in SPACESUIT state.
    private readonly float X_ACCEL_RATE = 5f;
    private readonly float Y_ACCEL_RATE = 5f;
    private readonly float Z_ACCEL_RATE = 5f;

    private readonly float X_ROLL_ACCEL_RATE = 5f;
    private readonly float Y_ROLL_ACCEL_RATE = 5f;
    private readonly float Z_ROLL_ACCEL_RATE = 5f;

    private readonly float CAMERA_SENSITIVITY = 1f;

    // TODO: Formalize this better.
    private readonly float SPACESUIT_RCS_ANGULAR_DRAG = 0.9f;
    private readonly float SPACESUIT_RCS_POSITIONAL_DRAG = 0.9f;
    #endregion
    #region Static parameters defining movement in GRAVITY state.
    private readonly float X_FLAT_MOVEMENT_SPEED = 10f;
    private readonly float Y_FLAT_MOVEMENT_SPEED = 10f;
    private readonly float MAX_COMBINED_MOVEMENT_SPEED = 10f;

    // Maximum number of degrees/sec that the player may be rotated while reorienting to gravity.
    private static readonly float GRAVITY_DEGREES_PER_SECOND = 180f;

    private readonly float X_ROTATION_RATE = 500f;
    private readonly float CAMERA_TILT_RATE = 500f;
    private readonly float CAMERA_3P_TILT_UP_DEGREE_LIMIT = 30f;
    private readonly float CAMERA_3P_TILT_DOWN_DEGREE_LIMIT = 45f;
    private readonly float CAMERA_1P_TILT_UP_DEGREE_LIMIT = 85f;
    private readonly float CAMERA_1P_TILT_DOWN_DEGREE_LIMIT = 85f;

    private readonly float GROUND_CHECK_DISTANCE = 1.5f;
    private readonly float JUMP_IMPULSE = 8f;
    #endregion
    #region Static parameters defining movement in TETHERING state.
    private static readonly float MAX_TETHER_LENGTH = 50f; // m
    private static readonly float TETHER_GAP_CLOSE_RATE = 100f; // m/s
    private static readonly float TETHER_GAP_CLOSE_END_DIST = 3f; // m
    private static readonly float CAMERA_ALIGN_RATE = 500f;
    #endregion

    #region Public members
    public TextMesh debugStateText;
    public Camera playerCamera_1P;
    public Camera playerCamera_3P;
    #endregion

    public enum MovementState { GRAVITY, SPACESUIT, TETHERING }
    private MovementState movementState = MovementState.SPACESUIT;
    private bool movementDisabled = false;

    private GameObject tetherAnchorTarget;

    private Rigidbody rigidbody;
    private Camera activeCamera;
    private List<GravityZone> gravitySources = new List<GravityZone>();
    private float cameraTiltUpDegreeLimit;
    private float cameraTiltDownDegreeLimit;
    private float prevFrameCameraTilt = 0f;
    private bool rcsOn = true;

    private float health = 5f;
    public class GravityZone : MonoBehaviour { };
    protected bool IsLocalPlayer = true;

    // Start is called before the first frame update
    void Start() {
        rigidbody = GetComponent<Rigidbody>();

        if (IsLocalPlayer) {
            StartLocalPlayer();
        } else {
            StartRemotePlayer();
        }

        tetherAnchorTarget = new GameObject("TetherTarget");
        // Parent to this player for organization, but will reparent to taret object when TETHERING.
        tetherAnchorTarget.transform.parent = this.transform.parent;
    }

    void OnDestroy() {
        ClearGravitySources();
    }

    private void StartLocalPlayer() {
        SetupFirstPersonCamera();
    }

    private void StartRemotePlayer() {
        playerCamera_1P.gameObject.SetActive(false);
        playerCamera_3P.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update() {
        UpdateCommon();
        if (IsLocalPlayer) {
            UpdateLocalPlayer();
        } else {
            // UpdateRemotePlayer();
        }
    }

    public void DisableMovement() {
        movementDisabled = true;
    }

    public void EnableMovement() {
        movementDisabled = false;
    }

    public void AddGravitySource(GravityZone gravityZone) {
        if (!gravitySources.Contains(gravityZone)) {
            gravitySources.Add(gravityZone);
        }
    }

    public void RemoveGravitySource(GravityZone gravityZone) {
        gravitySources.Remove(gravityZone);
    }

    public void ClearGravitySources() {
        foreach (GravityZone zone in gravitySources) {
            // zone.RemoveAffectedObject(rigidbody);
        }
        gravitySources.Clear();
    }

    private void AlignToWeightedAverageGravityFields() {
        if (gravitySources.Count == 0) {
            return;
        }

        Vector3 vectorSum = Vector3.zero;
        foreach (GravityZone zone in gravitySources) {
            float zoneDist = Vector3.Distance(transform.position, zone.transform.position);
            // vectorSum += (zone.GetGravityDirection(this.gameObject) * zone.GetGravityFieldStrength(this.gameObject));
        }
        Vector3 weightedGravityDirection = vectorSum.normalized;

        Vector3 targetPlayerUp = -weightedGravityDirection;
        Vector3 targetPlayerForward = Vector3.Cross(this.transform.right, targetPlayerUp);
        Quaternion targetOrientation = Quaternion.LookRotation(targetPlayerForward, targetPlayerUp);
        Quaternion newPlayerRotation = Quaternion.RotateTowards(this.transform.rotation, targetOrientation, Time.deltaTime * GRAVITY_DEGREES_PER_SECOND);


        // Try to keep camera tilted at the same angle while rotating the player's body in new gravity.

        // Actually had to learn more about quaternions to understand how to use them to avoid gimble lock
        // while doing this (sometimes X, Y, or Z will suddenly snap to a different value as Quaternion rotates).
        // We need to calculate the delta between the two quaternions and find the portion of that delta that occurs in the 
        // local X axis of the player+camera and apply it in reverse to the camera.

        // Try to keep camera tilted at the same angle while rotating the player's body in new gravity.
        // playerController.RotateWithFixedCamera(newPlayerRotation);
        this.transform.rotation = newPlayerRotation;

        // Quaternion delta = playerController.transform.rotation * Quaternion.Inverse(newPlayerRotation);
        // playerController.transform.rotation = newPlayerRotation;
        // playerController.RotateCamera(Quaternion.Inverse(delta));
    }

    // Should only be called by ServerRPC methods, so only runs on server.
    public void TakeDamage(float damage) {
        health -= damage;
        if (health < 0f) {
            health = 0f;
            Vector3 respawnPosition = new Vector3(Random.Range(0f, 10f), 0f, Random.Range(0f, 10f));
            // InvokeClientRpcOnEveryone(ClientDie, respawnPosition);
        }
    }

    public void RotateWithFixedCamera(Quaternion targetRotation) {
        Quaternion oldCameraRotation = activeCamera.transform.rotation;
        Vector3 oldCameraForward = activeCamera.transform.forward;
        transform.rotation = targetRotation;
        activeCamera.transform.rotation = oldCameraRotation;
    }

    public void TiltCamera(float tiltChange) {
        Vector3 cameraLocalRotation = activeCamera.transform.localEulerAngles;
        float camTilt = cameraLocalRotation.x + tiltChange;

        // camTilt = TransformUtils.ClampAngleDegree(camTilt, -cameraTiltUpDegreeLimit, cameraTiltDownDegreeLimit);
        cameraLocalRotation.x = camTilt;
        activeCamera.transform.localEulerAngles = cameraLocalRotation;
    }
    private void UpdateCommon() {

    }

    private void UpdateLocalPlayer() {
        // TODO(grantking): Should clean this up, even before switching to actual Input system.
        float xMoveInput = movementDisabled ? 0f : Input.GetAxis("Horizontal");
        float yMoveInput = movementDisabled ? 0f : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.Space)) ? 1f : (Input.GetKey(KeyCode.LeftControl) ? -1 : 0);
        float zMoveInput = movementDisabled ? 0f : Input.GetAxis("Vertical");

        float xLookInput = movementDisabled ? 0f : Input.GetAxis("Mouse X");
        float yLookInput = movementDisabled ? 0f : -Input.GetAxis("Mouse Y");
        float zLookInput = movementDisabled ? 0f : Input.GetKey(KeyCode.Q) ? 1f : (Input.GetKey(KeyCode.E) ? -1 : 0);

        bool jumpPressed = movementDisabled ? false : Input.GetKeyDown(KeyCode.Space);

        bool digPressed = Input.GetMouseButtonDown(0);
        bool tetherPressed = Input.GetMouseButtonDown(1);

        Vector3 rotationChange = Vector3.zero;

        switch (movementState) {
            case MovementState.GRAVITY:
                if (!InGravityZone()) {
                    SetMovementState(MovementState.SPACESUIT);
                    break;
                }

                if (digPressed && TryDigGround()) {
                    // SetMovementState(MovementState.TETHERING);
                    break;
                }

                if (tetherPressed && TryFireTether()) {
                    SetMovementState(MovementState.TETHERING);
                    break;
                }

                if (jumpPressed && IsGrounded()) {
                    rigidbody.AddForce(transform.up * JUMP_IMPULSE, ForceMode.Impulse);
                }
                Vector3 localVelocity = transform.InverseTransformVector(rigidbody.velocity);

                Vector3 newLocalVelocityXZ = new Vector3(
                    xMoveInput * X_FLAT_MOVEMENT_SPEED,
                    0f,
                    zMoveInput * Y_FLAT_MOVEMENT_SPEED
                );
                if (newLocalVelocityXZ.magnitude > MAX_COMBINED_MOVEMENT_SPEED) {
                    newLocalVelocityXZ = newLocalVelocityXZ.normalized * MAX_COMBINED_MOVEMENT_SPEED;
                }
                // Keep old Y component after normalizing XZ.
                newLocalVelocityXZ.y = localVelocity.y;
                rigidbody.velocity = transform.TransformVector(newLocalVelocityXZ);

                rotationChange = new Vector3(
                    0f,
                    xLookInput * X_ROTATION_RATE * Time.deltaTime,
                    0f
                );
                rigidbody.transform.Rotate(rotationChange, Space.Self);

                AlignToWeightedAverageGravityFields();
                TiltCamera(yLookInput * CAMERA_TILT_RATE * Time.deltaTime);
                break;

            case MovementState.SPACESUIT:
                if (InGravityZone()) {
                    SetMovementState(MovementState.GRAVITY);
                    break;
                }

                if (digPressed && TryDigGround()) {
                    // SetMovementState(MovementState.TETHERING);
                    break;
                }

                if (tetherPressed && TryFireTether()) {
                    SetMovementState(MovementState.TETHERING);
                    break;
                }

                Vector3 velocity = rigidbody.velocity;
                velocity += transform.right * xMoveInput * X_ACCEL_RATE * Time.deltaTime;
                velocity += transform.up * yMoveInput * Y_ACCEL_RATE * Time.deltaTime;
                velocity += transform.forward * zMoveInput * Z_ACCEL_RATE * Time.deltaTime;

                rotationChange = new Vector3(
                    yLookInput * CAMERA_SENSITIVITY * Y_ROLL_ACCEL_RATE * Time.deltaTime,
                    xLookInput * CAMERA_SENSITIVITY * X_ROLL_ACCEL_RATE * Time.deltaTime,
                    zLookInput * CAMERA_SENSITIVITY * Z_ROLL_ACCEL_RATE * Time.deltaTime
                );

                if (rcsOn) {
                    rigidbody.angularDrag = SPACESUIT_RCS_ANGULAR_DRAG;
                    velocity *= (1f - (SPACESUIT_RCS_POSITIONAL_DRAG * Time.deltaTime));
                } else {
                    rigidbody.angularDrag = 0f;
                }

                rigidbody.angularVelocity += transform.TransformVector(rotationChange);
                rigidbody.velocity = velocity;

                // Maintain camera rotation while rotating parent player body object to be zeroed on camera rotation.
                // The camera can only rotate around the X axis to look up and down, so this is the only axis needing adjustment.
                // This essentially resets the player's "body" to be looking forward instead of up/down while in spacesuit mode.

                float playerTiltDelta = activeCamera.transform.localEulerAngles.x; // only works if camera rotation is constrainted already.
                transform.RotateAround(activeCamera.transform.position, transform.right, playerTiltDelta);
                activeCamera.transform.Rotate(-Vector3.right * playerTiltDelta, Space.Self);
                break;

            case MovementState.TETHERING:
                rigidbody.velocity = Vector3.zero;
                Vector3 vectorToTarget = tetherAnchorTarget.transform.position - this.transform.position;
                Quaternion targetRotation = Quaternion.LookRotation(vectorToTarget, activeCamera.transform.up);
                this.transform.rotation = Quaternion.RotateTowards(this.transform.rotation, targetRotation, CAMERA_ALIGN_RATE * Time.deltaTime);
                float distanceToTarget = vectorToTarget.magnitude;
                if (distanceToTarget < TETHER_GAP_CLOSE_END_DIST) {
                    SetMovementState(MovementState.SPACESUIT);
                    break;
                }
                AlignToWeightedAverageGravityFields();
                rigidbody.MovePosition(this.transform.position + vectorToTarget.normalized * TETHER_GAP_CLOSE_RATE * Time.deltaTime);
                break;
        }
        debugStateText.text = string.Format("State: {0}", movementState);
    }

    private void SetupFirstPersonCamera() {
        activeCamera = playerCamera_1P;
        cameraTiltUpDegreeLimit = CAMERA_1P_TILT_UP_DEGREE_LIMIT;
        cameraTiltDownDegreeLimit = CAMERA_1P_TILT_DOWN_DEGREE_LIMIT;
    }
    private void SetupThirdPersonCamera() {
        activeCamera = playerCamera_3P;
        cameraTiltUpDegreeLimit = CAMERA_3P_TILT_UP_DEGREE_LIMIT;
        cameraTiltDownDegreeLimit = CAMERA_3P_TILT_DOWN_DEGREE_LIMIT;
    }

    private bool InGravityZone() {
        return gravitySources.Count > 0;
    }
    private bool IsGrounded() {
        return false;
        // return Physics.Raycast(transform.position, -transform.up, GROUND_CHECK_DISTANCE, LayerUtils.GroundLayermask);
    }
    private void SetMovementState(MovementState newState) {
        ExitState(movementState);
        movementState = newState;
        EnterState(movementState);
    }

    private void ExitState(MovementState oldState) {
        switch (oldState) {
            case MovementState.GRAVITY:
                break;
            case MovementState.SPACESUIT:
                break;
        }
    }
    private void EnterState(MovementState newState) {
        switch (newState) {
            case MovementState.GRAVITY:
                // Remove any existing velocity so that gravity takes immediate effect
                rigidbody.velocity = Vector3.zero;
                break;
            case MovementState.SPACESUIT:
                break;
        }
    }

    private bool TryDigGround() {
        Vector3 eyePos = activeCamera.transform.position;
        Vector3 forwardLook = activeCamera.transform.forward;
        Debug.DrawLine(eyePos, eyePos + forwardLook * MAX_TETHER_LENGTH, Color.red, 0.5f);
        RaycastHit hitInfo;
        if (Physics.Raycast(eyePos, forwardLook, out hitInfo, MAX_TETHER_LENGTH, LayerUtils.GroundLayermask, QueryTriggerInteraction.Ignore)) {
            MarchingCubesChunk chunk = hitInfo.collider.gameObject.GetComponent<MarchingCubesChunk>();
            if (chunk) {
                chunk.ReceivePlayerClick(hitInfo.point);
            }
            return true;
        }
        return false;
    }

    private bool TryFireTether() {
        //Vector3 eyePos = activeCamera.transform.position;
        //Vector3 forwardLook = activeCamera.transform.forward;
        //Debug.DrawLine(eyePos, eyePos + forwardLook * MAX_TETHER_LENGTH, Color.red, 0.5f);
        //RaycastHit hitInfo;
        //if (Physics.Raycast(eyePos, forwardLook, out hitInfo, MAX_TETHER_LENGTH, LayerUtils.GroundLayermask, QueryTriggerInteraction.Ignore)) {
        //    tetherAnchorTarget.transform.position = hitInfo.point;
        //    tetherAnchorTarget.transform.parent = hitInfo.transform;
        //    return true;
        //}
        return false;
    }

    private void ClientDie(Vector3 respawnPosition) {
        GetComponentInChildren<MeshRenderer>().enabled = false;
        GetComponentInChildren<Collider>().enabled = false;
        ClearGravitySources();

        StartCoroutine(RespawnWithDelay(respawnPosition, 5f));
    }

    private void Respawn(Vector3 respawnPosition) {
        health = 100f;
        this.transform.position = respawnPosition;
        GetComponentInChildren<MeshRenderer>().enabled = true;
        GetComponentInChildren<Collider>().enabled = true;
    }

    IEnumerator RespawnWithDelay(Vector3 respawnPosition, float delay) {
        yield return new WaitForSeconds(delay);
        // InvokeServerRpc(Respawn, respawnPosition);
    }
}