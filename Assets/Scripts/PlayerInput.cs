#region

using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;
using UnityEngine.InputSystem;

#endregion

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

        // We need to store current input to compare against previous input (to track actions activation/deactivation). It is also used if the input for current tick is not available.
        // This is not needed on proxies and will be replicated to input authority only.
        [Networked] private GameplayInput _currentInput { get; set; }

        /// <summary>
        ///     3. Read input from Fusion.
        /// </summary>
        void IBeforeTick.BeforeTick()
        {
            if (Object == null)
                return;

            // Set current in input as previous.
            PreviousInput = _currentInput;

            // Clear all properties which should not propagate from last known input in case of missing new input. As example, following line will reset look rotation delta.
            // This results to the player not being incorrectly rotated (by using rotation delta from last known input) in case of missing input on state authority, followed by a correction on the input authority.
            var currentInput = _currentInput;
            currentInput.LookRotationDelta = default;
            _currentInput = currentInput;

            if (Object.InputAuthority != PlayerRef.None)
                // If this fails, the current input won't be updated and input from previous tick will be reused.
                if (GetInput(out GameplayInput input))
                    // New input received, we can store it as current.
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

            // Accumulated input was polled and explicit reset requested.
            if (_resetAccumulatedInput)
            {
                _resetAccumulatedInput = false;
                _accumulatedInput = default;
            }

            if (Application.isMobilePlatform == false || Application.isEditor)
                // Input is tracked only if the cursor is locked.
                if (Cursor.lockState != CursorLockMode.Locked)
                    return;

            // Always use KeyControl.isPressed, Input.GetMouseButton() and Input.GetKey().
            // Never use KeyControl.wasPressedThisFrame, Input.GetMouseButtonDown() or Input.GetKeyDown() otherwise the action might be lost.

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
            // Reset to default state.
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
                    // Hide cursor
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // Only local player needs networked properties (current input).
            // This saves network traffic by not synchronizing networked properties to other clients except local player.
            ReplicateToAll(false);
            ReplicateTo(Object.InputAuthority, true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (runner == null)
                return;

            var networkEvents = runner.GetComponent<NetworkEvents>();
            if (networkEvents != null)
                // Unregister local player input polling.
                networkEvents.OnInput.RemoveListener(OnInput);
        }

        /// <summary>
        ///     2. Push accumulated input and reset properties, can be executed multiple times within single Unity frame if the
        ///     rendering speed is slower than Fusion simulation.
        ///     This is usually executed multiple times if there is a performance spike, for example after expensive spawn which
        ///     includes asset loading.
        /// </summary>
        private void OnInput(NetworkRunner runner, NetworkInput networkInput)
        {
            // Mouse movement (delta values) is aligned to engine update.
            // To get perfectly smooth interpolated look, we need to align the mouse input with Fusion ticks.
            _accumulatedInput.LookRotationDelta = _lookRotationAccumulator.ConsumeTickAligned(runner);

            // Set accumulated input.
            networkInput.Set(_accumulatedInput);

            // Input is polled for single fixed update, but at this time we don't know how many times in a row OnInput() will be executed.
            // This is the reason to have a reset flag instead of resetting input immediately, otherwise we could lose input for next fixed updates (for example move direction).
            _resetAccumulatedInput = true;
        }
    }