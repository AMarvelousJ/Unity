using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZCJ.Shiploader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ShiploaderWalkthrough : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float fastSpeed = 14f;
        [SerializeField] private float lookSpeed = 100f;
        [SerializeField] private float eyeHeight = 1.7f;
        private ShiploaderOrbitCamera orbit;
        private Camera view;
        private CharacterController body;
        private readonly List<Collider> ownedColliders = new();
        private bool collisionReady;
        private GameObject collisionRoot;
        private float heading, elevation, verticalSpeed;
        private float previousFov, previousNear;
        private ShiploaderCameraPreset previousPreset;
        private Vector3 spawn;
        private readonly List<ClimbRoute> ladders = new();
        private ClimbRoute climbing;
        private float climbProgress;
        private Transform support;
        private Vector3 supportLocal;
        private sealed class ClimbRoute
        {
            public Transform frame;
            public Vector3 bottom, top;
            public Vector3 stairStart, stairEnd;
            public Vector3 Bottom => frame.TransformPoint(bottom);
            public Vector3 Top => frame.TransformPoint(top);
            public Vector3 Position(float t) => frame.TransformPoint(t < .1f ? Vector3.Lerp(bottom, stairStart, t * 10) : t < .9f ? Vector3.Lerp(stairStart, stairEnd, (t - .1f) / .8f) : Vector3.Lerp(stairEnd, top, (t - .9f) * 10));
        }
        public bool IsClimbing => climbing != null;
        public string ClimbHint => IsClimbing ? "左摇杆上下：爬梯　X / A：松开" : "A：跳跃　靠近梯脚或梯顶按 X：爬梯";

        public bool Active { get; private set; }
        public string Hint { get; private set; } = "";
        public bool HasGamepad => Gamepad.current != null && Gamepad.current.added;
        public Vector3 FeetPosition => body != null ? body.transform.position : Vector3.zero;

        public void Configure(ShiploaderOrbitCamera cameraController)
        {
            orbit = cameraController;
            view = GetComponent<Camera>();
        }

        public bool Enter()
        {
            if (Active) return true;
            FindFirstObjectByType<ShiploaderFleetController>()?.SuspendInput();
            if (orbit == null) Configure(GetComponent<ShiploaderOrbitCamera>());
            BuildWalkingCollisions();
            if (collisionRoot != null) collisionRoot.SetActive(true);
            foreach (var collider in ownedColliders) if (collider != null) collider.enabled = true;
            if (body == null)
            {
                var player = new GameObject("Runtime_HarborWalker");
                body = player.AddComponent<CharacterController>();
                body.height = 1.8f; body.radius = .3f;
                body.center = Vector3.up * .9f;
                body.stepOffset = .3f; body.slopeLimit = 45;
                body.skinWidth = .035f; body.minMoveDistance = 0;
                body.enabled = false;
            }
            Vector3 rigPosition = orbit != null && orbit.WorkingRig != null ? orbit.WorkingRig.transform.position : Vector3.zero;
            Vector3 candidate = rigPosition + new Vector3(-16, 0, -10);
            Physics.SyncTransforms();
            if (!Physics.Raycast(candidate + Vector3.up * 8, Vector3.down, out RaycastHit hit, 12, ~0, QueryTriggerInteraction.Ignore))
            {
                Hint = "未找到可行走地面，请打开港口场景。";
                return false;
            }
            spawn = hit.point + Vector3.up * .05f;
            previousPreset = orbit.Preset;
            previousFov = view.fieldOfView; previousNear = view.nearClipPlane;
            view.orthographic = false; view.fieldOfView = 65; view.nearClipPlane = .08f;
            Active = true;
            ReturnToStart();
            Hint = "";
            return true;
        }

        public void Exit(bool restorePreset = true)
        {
            if (!Active) return;
            Active = false;
            climbing = null; support = null;
            if (body != null) body.enabled = false;
            if (collisionRoot != null) collisionRoot.SetActive(false);
            foreach (var collider in ownedColliders) if (collider != null) collider.enabled = false;
            view.fieldOfView = previousFov; view.nearClipPlane = previousNear;
            if (restorePreset && orbit != null) orbit.SetPreset(previousPreset);
        }

        public void Toggle() { if (Active) Exit(); else Enter(); }

        public void ReturnToStart()
        {
            if (!Active || body == null) return;
            body.enabled = false; body.transform.position = spawn; body.enabled = true;
            heading = 90; elevation = 0; verticalSpeed = 0;
            climbing = null; support = null;
            UpdateView();
        }

        private void Update()
        {
            // The editor's Scene view must never consume movement intended for the Game view.
            if (!Application.isFocused) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorWindow.focusedWindow == null || UnityEditor.EditorWindow.focusedWindow.GetType().Name != "GameView") return;
#endif
            Gamepad pad = Gamepad.current;
            Keyboard keyboard = Keyboard.current;
            if ((pad != null && pad.startButton.wasPressedThisFrame && (Active || FindFirstObjectByType<ShiploaderFleetController>() == null)) || (keyboard != null && keyboard.f8Key.wasPressedThisFrame))
            { Toggle(); return; }
            if (!Active) return;
            if ((pad != null && pad.buttonEast.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            { Exit(); return; }
            if ((pad != null && pad.buttonNorth.wasPressedThisFrame) || (keyboard != null && keyboard.rKey.wasPressedThisFrame))
            { ReturnToStart(); return; }

            Vector2 move = pad != null ? ApplyDeadzone(pad.leftStick.ReadUnprocessedValue()) : Vector2.zero;
            Vector2 look = pad != null ? ApplyDeadzone(pad.rightStick.ReadUnprocessedValue()) : Vector2.zero;
            bool fast = pad != null && pad.rightTrigger.ReadValue() > .25f;
            if (keyboard != null)
            {
                move += new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                fast |= keyboard.leftShiftKey.isPressed;
            }
            bool jump = (pad != null && pad.buttonSouth.wasPressedThisFrame) || (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            bool interact = (pad != null && pad.buttonWest.wasPressedThisFrame) || (keyboard != null && keyboard.eKey.wasPressedThisFrame);
            SimulateInput(move, look, fast, Mathf.Min(Time.unscaledDeltaTime, .05f), jump, interact);
        }

        // Kept independent of device polling so movement/collision can be checked deterministically.
        public void SimulateInput(Vector2 move, Vector2 look, bool fast, float deltaTime, bool jump = false, bool interact = false)
        {
            if (!Active || deltaTime <= 0) return;
            float dt = Mathf.Min(deltaTime, .05f);
            bool groundedBeforeCarry = body.enabled && body.isGrounded;
            heading += look.x * lookSpeed * dt;
            elevation = Mathf.Clamp(elevation - look.y * lookSpeed * dt, -80, 80);
            if (support != null && climbing == null)
            {
                Vector3 carry = support.TransformPoint(supportLocal) - body.transform.position;
                if (carry.sqrMagnitude > .000001f) body.Move(carry);
            }
            if (interact)
            {
                if (IsClimbing) ReleaseLadder();
                else TryClimb();
            }
            if (IsClimbing)
            {
                if (jump) ReleaseLadder();
                else
                {
                    climbProgress = Mathf.Clamp01(climbProgress + move.y * 2.5f * dt / Vector3.Distance(climbing.Bottom, climbing.Top));
                    body.transform.position = climbing.Position(climbProgress);
                    if ((climbProgress >= 1 && move.y > 0) || (climbProgress <= 0 && move.y < 0)) ReleaseLadder();
                    UpdateView(); return;
                }
            }
            Vector3 horizontal = Quaternion.Euler(0, heading, 0) * new Vector3(move.x, 0, move.y);
            horizontal = Vector3.ClampMagnitude(horizontal, 1) * (fast ? fastSpeed : walkSpeed) * dt;
            Vector3 before = body.transform.position;
            // A support probe prevents stepping off the quay into the sea. Never use water as ground.
            Vector3 next = before + horizontal;
            if (!Physics.Raycast(next + Vector3.up * .5f, Vector3.down, 160f, ~0, QueryTriggerInteraction.Ignore))
                horizontal = Vector3.zero;
            bool grounded = body.isGrounded || groundedBeforeCarry;
            verticalSpeed = jump && grounded ? Mathf.Sqrt(2 * 18 * 1.25f) : grounded && verticalSpeed <= 0 ? -2 : Mathf.Max(-12, verticalSpeed - 18 * dt);
            body.Move(horizontal + Vector3.up * (verticalSpeed * dt));
            if ((body.collisionFlags & CollisionFlags.Above) != 0) verticalSpeed = Mathf.Min(0, verticalSpeed);
            support = null;
            if (body.isGrounded && Physics.Raycast(FeetPosition + Vector3.up * .1f, Vector3.down, out var floor, .3f, ~0, QueryTriggerInteraction.Ignore))
            { support = floor.transform; supportLocal = support.InverseTransformPoint(FeetPosition); }
            if (body.transform.position.y < -1 || body.transform.position.y > 150) ReturnToStart();
            UpdateView();
        }

        private void TryClimb()
        {
            float nearest = 2.2f;
            foreach (var route in ladders)
            {
                if (route.frame == null) continue;
                float bottomDistance = Vector3.Distance(FeetPosition, route.Bottom);
                float topDistance = Vector3.Distance(FeetPosition, route.Top);
                float distance = Mathf.Min(bottomDistance, topDistance);
                if (distance >= nearest) continue;
                nearest = distance; climbing = route; climbProgress = bottomDistance < topDistance ? 0 : 1;
            }
            if (!IsClimbing) return;
            support = null; verticalSpeed = 0; body.enabled = false;
            body.transform.position = climbing.Position(climbProgress);
        }

        private void ReleaseLadder()
        {
            climbing = null; verticalSpeed = 0; body.enabled = true;
        }

        public static Vector2 ApplyDeadzone(Vector2 value)
        {
            const float deadzone = .16f;
            float magnitude = value.magnitude;
            return magnitude <= deadzone ? Vector2.zero : value / magnitude * Mathf.Clamp01((magnitude - deadzone) / (1 - deadzone));
        }

        private void UpdateView()
        {
            transform.SetPositionAndRotation(body.transform.position + Vector3.up * eyeHeight,
                Quaternion.Euler(elevation, heading, 0));
        }

        private void BuildWalkingCollisions()
        {
            if (collisionReady) return;
            var env = GameObject.Find("PortEnvironment");
            if (env == null) return;
            collisionRoot = new GameObject("Runtime_WalkablePort");
            // Render meshes are statically batched in these scenes. Reusing their combined
            // meshes as colliders would duplicate whole batches at the wrong transforms.
            // Use inexpensive collision proxies for the authored ground and major obstacles.
            foreach (var renderer in env.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.enabled && renderer.name == "ConcreteApron")
                    AddCollisionBox("PierGround", renderer.bounds.center, renderer.bounds.size);
            }
            if (env.transform.Find("AerialReferencePort") != null)
            {
                AddCollisionBox("MainLand", new Vector3(-387,-1.25f,-130), new Vector3(614,2.5f,520));
                AddCollisionBox("SouthLand", new Vector3(-66,-1.25f,-330), new Vector3(28,2.5f,120));
                foreach (float z in new[] {0f,-200f,-266f})
                    AddCollisionBox("PierApproach", new Vector3(-69,-.8f,z), new Vector3(25,1.6f,27));
                for (int row=0;row<9;row++)
                    AddCollisionBox("StockyardBoundary", new Vector3(-410,5,-65-row*34), new Vector3(495,10,25));
                for (int row=0;row<4;row++)
                {
                    AddCollisionBox("StorageShed", new Vector3(-323,4,12+row*17), new Vector3(260,8,14));
                    AddCollisionBox("ServiceBuilding", new Vector3(-150,3,28+row*24), new Vector3(25,6,17));
                }
                AddCollisionBox("PondBoundary", new Vector3(-572,1,43), new Vector3(160,2,100));
            }
            else
            {
                AddCollisionBox("ShoreGround", new Vector3(-150,-1.05f,0), new Vector3(140,2.1f,600));
                AddCollisionBox("PierApproach", new Vector3(-73,-.8f,0), new Vector3(22,1.6f,27));
            }
            BuildLadders();
            collisionReady = true;
        }

        private void BuildLadders()
        {
            // These stair meshes were merged by the model builder. Reconstruct their
            // authored route in the portal frame, so moving loaders carry it with them.
            foreach (var mesh in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (mesh.name != "Portal_OxideRed") continue;
                var frame = mesh.transform.parent;
                var rig = frame.GetComponentInParent<ShiploaderRigController>();
                var config = rig != null ? rig.Config : orbit.WorkingRig.Config;
                float h = config.baseHeight - .75f, g = config.railGauge * .5f, w = config.wheelBase * .5f;
                ladders.Add(new ClimbRoute { frame = frame,
                    bottom = new Vector3(-w, .05f, g - 1.3f),
                    stairStart = new Vector3(-w,1.55f,g-1.3f),
                    stairEnd = new Vector3(-w+8,h+.95f,g-1.3f),
                    top = new Vector3(-w + 8, h + .92f, g - .4f) });
                foreach (float z in new[] {-g, g})
                    AddPortalFloor(frame, new Vector3(0,h+.8f,z),new Vector3(config.wheelBase+2,.14f,1.9f));
                foreach (float sign in new[] {-1f,1f})
                {
                    AddPortalFloor(frame,new Vector3(sign*5.15f,h+.85f,0),new Vector3(3.2f,.14f,15.2f));
                    AddPortalFloor(frame,new Vector3(0,h+.85f,sign*5.85f),new Vector3(7.1f,.14f,3.5f));
                }
            }
        }

        private void AddPortalFloor(Transform frame, Vector3 center, Vector3 size)
        {
            var node = new GameObject("Runtime_PortalFloor");
            node.transform.SetParent(frame, false); node.transform.localPosition = center;
            var collider = node.AddComponent<BoxCollider>(); collider.size = size;
            ownedColliders.Add(collider);
        }

        private void AddCollisionBox(string name, Vector3 center, Vector3 size)
        {
            var node = new GameObject(name); node.transform.SetParent(collisionRoot.transform, false);
            node.transform.position = center;
            var collider = node.AddComponent<BoxCollider>(); collider.size = size;
            ownedColliders.Add(collider);
        }

        private void OnDisable() { if (Active) Exit(); }
        private void OnDestroy()
        {
            foreach (var collider in ownedColliders) if (collider != null && collider.name == "Runtime_PortalFloor") Destroy(collider.gameObject);
            if (body != null) Destroy(body.gameObject);
            if (collisionRoot != null) Destroy(collisionRoot);
        }
    }
}
