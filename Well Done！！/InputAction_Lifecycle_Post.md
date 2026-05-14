# What We Learned: The InputAction Lifecycle in Unity

**Group:** Well Done!!

**Scripts studied:** `Controller.cs`, `CameraController.cs`, `UIManager.cs`, `ShootingController.cs`

## New Concept

We learned that `InputAction` is part of Unity's new Input System. Instead of hard-coding one specific key check directly in the script, an `InputAction` represents a gameplay action such as `Move`, `Look`, `Fire`, or `Pause`.

In this project, the `InputAction` fields are embedded inside `MonoBehaviour` components and saved as scene or prefab data. For example, the movement action is bound to WASD and arrow keys, the look action is bound to mouse position, the fire action is bound to left mouse button and space, and the pause action is bound to Escape and P.

The main question from the lab table was:

**Why are actions enabled in `OnEnable` and disabled in `OnDisable`?**

The answer is that Unity calls `OnEnable()` when a component becomes active, so this is the correct time for the script to start listening for input. Unity calls `OnDisable()` when the component stops being active, so the script should disable its actions at that point to stop listening for input. This prevents disabled or inactive objects from still responding to player input.

## Code Evidence

### `Controller.cs`

In `Controller.cs`, the player controller has two public `InputAction` fields for movement and looking:

```csharp
public InputAction moveAction;
public InputAction lookAction;
```

These actions are enabled and disabled with the player controller component:

```csharp
void OnEnable()
{
    moveAction.Enable();
    lookAction.Enable();
}

void OnDisable()
{
    moveAction.Disable();
    lookAction.Disable();
}
```

Then, during `Update()`, the script reads the current movement action value:

```csharp
void Update()
{
    HandleInput();
}

private void HandleInput()
{
    Vector2 moveInput = moveAction.ReadValue<Vector2>();
    Vector3 movementVector = new Vector3(moveInput.x, moveInput.y, 0);

    MovePlayer(movementVector);
    LookAtPoint(GetLookPosition());
}
```

This shows that `OnEnable()` and `OnDisable()` control whether the movement and look actions are listening for input, while `Update()` uses the action values every frame to move and rotate the player.

### `CameraController.cs`

In `CameraController.cs`, the camera also has an embedded `InputAction` for looking:

```csharp
public InputAction lookAction;
```

The camera enables and disables this action with its own component lifecycle:

```csharp
void OnEnable()
{
    lookAction.Enable();
}

void OnDisable()
{
    lookAction.Disable();
}
```

When the camera is active, it reads the current mouse position from the action:

```csharp
public Vector3 GetPlayerMousePosition()
{
    if (cameraMovementStyle == CameraStyles.Locked)
    {
        return Vector3.zero;
    }
    return playerCamera.ScreenToWorldPoint(lookAction.ReadValue<Vector2>());
}
```

This shows the same lifecycle pattern: the camera should only read from `lookAction` while the camera controller is enabled.

### `UIManager.cs`

In `UIManager.cs`, the pause input is stored as another `InputAction`:

```csharp
public InputAction pauseAction;
```

The UI manager enables and disables the pause action when the component becomes active or inactive:

```csharp
private void OnEnable()
{
    pauseAction.Enable();
}

private void OnDisable()
{
    pauseAction.Disable();
}
```

Then `Update()` checks whether the pause action was triggered:

```csharp
private void Update()
{
    CheckPauseInput();
}

private void CheckPauseInput()
{
    if (pauseAction.triggered)
    {
        TogglePause();
    }
}
```

This proves that the pause action follows the same rule as the player and camera actions: it starts listening in `OnEnable()` and stops listening in `OnDisable()`.

## Event Chain

Player movement event chain:

Unity enables the player object  
-> `Controller.OnEnable()`  
-> `moveAction` and `lookAction` begin listening for input  
-> Unity calls `Controller.Update()` every frame  
-> `HandleInput()` reads the current action values  
-> `MovePlayer()` changes the player position  
-> `LookAtPoint()` rotates the player toward the mouse  

The player sees the ship move with WASD or arrow keys and aim toward the mouse.

## Another Example: Pause Input

`UIManager.cs` uses the same idea for pausing:

```csharp
private void OnEnable()
{
    pauseAction.Enable();
}

private void OnDisable()
{
    pauseAction.Disable();
}

private void Update()
{
    CheckPauseInput();
}

private void CheckPauseInput()
{
    if (pauseAction.triggered)
    {
        TogglePause();
    }
}
```

Pause event chain:

Unity frame update  
-> `UIManager.Update()`  
-> `CheckPauseInput()`  
-> `pauseAction.triggered`  
-> `TogglePause()`  
-> the pause menu appears or disappears, and `Time.timeScale` changes  

This is also event-driven because Unity calls `Update()`, and then the script reacts to the current state of the input action.

## Why This Matters

In a normal Java program, we might look for `main()` and follow a loop written by the programmer. In Unity, the engine owns the game loop. Our scripts react when Unity sends lifecycle events such as `OnEnable()`, `Update()`, and `OnDisable()`.

`InputAction` fits this model because an action must be enabled before it can listen for input. This is cleaner and more flexible than hard-coding one key everywhere. The same action can support multiple bindings, such as WASD and arrow keys for movement.

## Improvement Idea

One possible improvement would be to make pause input use an `InputAction` callback instead of checking `pauseAction.triggered` every frame.

```csharp
private void OnEnable()
{
    pauseAction.performed += OnPausePerformed;
    pauseAction.Enable();
}

private void OnDisable()
{
    pauseAction.performed -= OnPausePerformed;
    pauseAction.Disable();
}

private void OnPausePerformed(InputAction.CallbackContext context)
{
    TogglePause();
}
```

This fits pause input well because pause is a single button event. Movement should probably still use `ReadValue<Vector2>()` in `Update()`, because movement needs a continuous input value every frame.

## Sources

- [Unity Input System Manual: Actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/Actions.html)
- [Unity Input System API: InputAction](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/api/UnityEngine.InputSystem.InputAction.html)
- [Unity Scripting API: MonoBehaviour.OnEnable](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnEnable.html)
- [Unity Scripting API: MonoBehaviour.OnDisable](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnDisable.html)
- [Unity Scripting API: MonoBehaviour.Update](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.Update.html)


## Reflection

The most important thing we learned is that `InputAction` is not just a simple keyboard check. It represents a logical input action, and it must be enabled before it can listen to its bound controls. In this project, Unity's lifecycle decides when the player, camera, and UI input actions are active, and the gameplay code reacts to those action values.
