# What we learned: MonoBehaviour lifecycle

Group: Well Done
Scripts studied: `GameManager.cs`, `UIManager.cs`, `Health.cs`, `Projectile.cs`

## New concept

We learned that Unity scripts do not usually run from a `main()` method that we write ourselves. Instead, Unity owns the game loop and calls special `MonoBehaviour` event functions when the script reaches certain moments in its lifetime.

The most important lifecycle functions we found were `Awake`, `OnEnable`, `Start`, `Update`, `OnDisable`, and `OnApplicationQuit`. We did not find a strong `LateUpdate()` example in these scripts, but we learned that it usually runs after `Update()` and is often useful for camera follow logic.

## Code evidence

In `GameManager.cs`, Unity calls `Awake()` before `Start()`. This script uses `Awake()` to set up the global `GameManager.instance` reference and find the player object before the game starts running normal setup code.

```csharp
private void Awake()
{
    if (instance == null)
    {
        instance = this;
    }
    else
    {
        DestroyImmediate(this);
    }
}

private void Start()
{
    HandleStartUp();
}
```

In `UIManager.cs`, Unity calls `OnEnable()` when the UI manager becomes active and `OnDisable()` when it becomes inactive. The script enables and disables the pause input action at the same time as the component.

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

In `Projectile.cs`, Unity calls `Update()` once every frame while the component is enabled. That makes the projectile move continuously.

```csharp
private void Update()
{
    MoveProjectile();
}
```

In `Health.cs`, `Start()` saves the first respawn point, and `Update()` keeps checking whether invincibility time has expired.

```csharp
void Start()
{
    SetRespawnPoint(transform.position);
}

void Update()
{
    InvincibilityCheck();
}
```

## Event chain

Unity creates or loads the scene object
-> Unity calls `Awake()`
-> initialization such as `GameManager.instance = this` happens
-> Unity calls `OnEnable()` if the object is active
-> input actions such as `pauseAction` are enabled
-> Unity calls `Start()` before the first frame update
-> setup methods such as `HandleStartUp()` and `SetRespawnPoint()` run
-> Unity calls `Update()` every frame
-> gameplay logic such as projectile movement, pause checking, and invincibility checking continues.

## Why this matters

This was useful for us because it changed how we read the scripts. Instead of looking for one main loop, we started by finding the Unity event functions first. A method like `MoveProjectile()` does not run by itself. It runs because Unity calls `Update()`, and `Update()` calls `MoveProjectile()`.

Choosing the right lifecycle method makes the code safer. `Awake()` is good for early references, `Start()` is good for setup before gameplay begins, `OnEnable()` and `OnDisable()` are good for turning input or subscriptions on and off, and `Update()` is good for logic that must be checked every frame.

## Improvement idea

One improvement would be to add comments or debug logs temporarily while learning the lifecycle order:

```csharp
private void Awake()
{
    Debug.Log("GameManager Awake");
}

private void Start()
{
    Debug.Log("GameManager Start");
}
```

This would make it easier to see the actual order in the Unity Console while the scene starts.

## Sources

- Unity Manual: Event function execution order: https://docs.unity3d.com/Manual/execution-order.html
- Unity Scripting API: MonoBehaviour: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
- Unity Learn: Awake and Start: https://learn.unity.com/tutorial/awake-and-start

## Reflection

The most important thing we learned is that Unity calls our script methods at specific times. We write the event functions, but Unity decides when they run. That is different from writing a normal C# console program, where our own code usually controls the main loop.
