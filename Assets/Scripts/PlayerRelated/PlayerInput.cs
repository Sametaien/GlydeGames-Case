#region

using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;
using UnityEngine.InputSystem;

#endregion

namespace PlayerRelated
{
    /// <summary>
    ///     Tracks player input.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class PlayerInput : NetworkBehaviour, IBeforeUpdate, IBeforeTick
    {
        [SerializeField] [Tooltip("Mouse delta multiplier.")]
        private Vector2 _lookSensitivity = Vector2.one;

        private readonly Vector2Accumulator _lookRotationAccumulator = new(0.02f, true);

        private GameplayInput _accumulatedInput;

        private bool _resetAccumulatedInput;
        public GameplayInput CurrentInput => _currentInput;
        public GameplayInput PreviousInput { get; private set; }

        // We need to store current input to compare against previous input (to track actions activation/deactivation). It is also used if the input for the current tick is not available.

        [Networked] private GameplayInput _currentInput { get; set; }

        void IBeforeTick.BeforeTick()
        {
            if (Object == null)
                return;

            // Set current in input as previous.
            PreviousInput = _currentInput;


            var currentInput = _currentInput;
            currentInput.LookRotationDelta = default;
            _currentInput = currentInput;

            if (Object.InputAuthority != PlayerRef.None)
                // If this fails, the current input won't be updated and input from the previous tick will be reused.
                if (GetInput(out GameplayInput input))
                    _currentInput = input;
        }

        /// <summary>
        ///     1. Collect input from devices, can be executed multiple times between FixedUpdateNetwork() calls because of faster
        ///     rendering speed.
        /// </summary>
        void IBeforeUpdate.BeforeUpdate()
        {
            if (HasInputAuthority == false)
                return;

            if (_resetAccumulatedInput)
            {
                _resetAccumulatedInput = false;
                _accumulatedInput = default;
            }

            if (Application.isMobilePlatform == false || Application.isEditor)
                // Input is tracked only if the cursor is locked.
                if (Cursor.lockState != CursorLockMode.Locked)
                    return;


            var mouse = Mouse.current;
            if (mouse != null)
            {
                var mouseDelta = mouse.delta.ReadValue();
                _lookRotationAccumulator.Accumulate(new Vector2(-mouseDelta.y, mouseDelta.x) * _lookSensitivity);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var moveDirection = Vector2.zero;

                if (keyboard.wKey.isPressed) moveDirection += Vector2.up;
                if (keyboard.sKey.isPressed) moveDirection += Vector2.down;
                if (keyboard.aKey.isPressed) moveDirection += Vector2.left;
                if (keyboard.dKey.isPressed) moveDirection += Vector2.right;

                _accumulatedInput.MoveDirection = moveDirection.normalized;

                _accumulatedInput.Actions.Set(GameplayInput.JUMP_BUTTON, keyboard.spaceKey.isPressed);
            }
        }

        public override void Spawned()
        {
            _currentInput = default;
            PreviousInput = default;
            _accumulatedInput = default;
            _resetAccumulatedInput = default;

            if (Object.HasInputAuthority)
            {
                // Register local player input polling.
                var networkEvents = Runner.GetComponent<NetworkEvents>();
                networkEvents.OnInput.AddListener(OnInput);

                if (Application.isMobilePlatform == false || Application.isEditor)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }


            ReplicateToAll(false);
            ReplicateTo(Object.InputAuthority, true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (runner == null)
                return;

            var networkEvents = runner.GetComponent<NetworkEvents>();
            if (networkEvents != null)
                networkEvents.OnInput.RemoveListener(OnInput);
        }

        /// <summary>
        ///     2. Push accumulated input and reset properties can be executed multiple times within single Unity frame if the
        ///     rendering speed is slower than Fusion simulation.
        /// </summary>
        private void OnInput(NetworkRunner runner, NetworkInput networkInput)
        {
            _accumulatedInput.LookRotationDelta = _lookRotationAccumulator.ConsumeTickAligned(runner);

            networkInput.Set(_accumulatedInput);

            _resetAccumulatedInput = true;
        }
    }
}