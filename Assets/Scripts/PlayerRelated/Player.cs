#region

using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;

#endregion

/// <summary>
/// Player class that handles player movement, health, and camera control.
/// </summary>
namespace PlayerRelated
{
    [DefaultExecutionOrder(-5)]
    public sealed class Player : NetworkBehaviour
    {
        public SimpleKCC KCC;
        public PlayerInput Input;
        public Transform CameraPivot;
        public Transform CameraHandle;

        [Header("Movement")] public float MoveSpeed = 10.0f;

        public float JumpImpulse = 10.0f;
        public float UpGravity = -25.0f;
        public float DownGravity = -40.0f;
        public float GroundAcceleration = 55.0f;
        public float GroundDeceleration = 25.0f;
        public float AirAcceleration = 25.0f;
        public float AirDeceleration = 1.3f;
        public float MaxHealth = 100f;
        private bool _applyKnockback;
        private Vector3 _pendingKnockback;

        [field: Header("Health")] [Networked] public float Health { get; set; } = 100f;

        [Networked] private Vector3 _moveVelocity { get; set; }

        private void Awake()
        {
            if (HasInputAuthority && FusionHUD.Instance != null) FusionHUD.Instance.UpdateHealth((int)Health);
        }

        private void LateUpdate()
        {
            if (HasInputAuthority == false) return;

            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
        }

        public override void FixedUpdateNetwork()
        {
            KCC.AddLookRotation(Input.CurrentInput.LookRotationDelta);

            var inputDirection = KCC.TransformRotation * new Vector3(Input.CurrentInput.MoveDirection.x, 0.0f,
                Input.CurrentInput.MoveDirection.y);
            float jumpImpulse = default;

            if (Input.CurrentInput.Actions.WasPressed(Input.PreviousInput.Actions, GameplayInput.JUMP_BUTTON))
                if (KCC.IsGrounded)
                    jumpImpulse = JumpImpulse;

            KCC.SetGravity(KCC.RealVelocity.y >= 0.0f ? UpGravity : DownGravity);

            var desiredMoveVelocity = inputDirection * MoveSpeed;

            if (KCC.ProjectOnGround(desiredMoveVelocity, out var projectedDesiredMoveVelocity))
                desiredMoveVelocity = Vector3.Normalize(projectedDesiredMoveVelocity) * MoveSpeed;

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
                acceleration = KCC.IsGrounded ? GroundDeceleration : AirDeceleration;
            else
                acceleration = KCC.IsGrounded ? GroundAcceleration : AirAcceleration;

            _moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

            KCC.Move(_moveVelocity, jumpImpulse);

            if (_applyKnockback)
            {
                KCC.Move(_pendingKnockback);
                _applyKnockback = false;
            }
        }

        // This method is called when the player takes damage.
        public void TakeDamage(float damage, Vector3 hitDirection, float knockbackForce)
        {
            if (!HasStateAuthority) return;

            Health = Mathf.Max(0, Health - damage);
            Debug.Log($"{gameObject.name} took {damage} damage. Remaining health: {Health}");

            if (KCC != null)
            {
                _pendingKnockback = -hitDirection.normalized * knockbackForce;
                _applyKnockback = true;
                Debug.Log($"{gameObject.name} queued knockback with force: {_pendingKnockback}");
            }

            RpcUpdateHealth(Health);

            //if (Health <= 0) RpcDie(); We can use for further damage checks
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcUpdateHealth(float newHealth)
        {
            if (HasInputAuthority && FusionHUD.Instance != null) FusionHUD.Instance.UpdateHealth((int)newHealth);
        }

        // This method is called when the player dies.
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcDie()
        {
            if (gameObject != null)
            {
                gameObject.SetActive(false); // Runner.Despawn is a better solution
                Debug.Log($"{gameObject.name} died.");
                if (HasInputAuthority && FusionHUD.Instance != null)
                    FusionHUD.Instance.LogEvent($"{Object.InputAuthority} died.");
            }
        }
    }
}