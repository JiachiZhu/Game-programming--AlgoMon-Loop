# What We Learned: PlayerPrefs Saves Score and High Score Between Play Sessions

**Group:** Well Done!!
**Scripts studied:** `GameManager.cs`

## New Concept

The concept we studied is `PlayerPrefs`.

We learned that `PlayerPrefs` is Unity’s simple local storage system. It can save small pieces of data on the player’s device, such as integers, floats, and strings. In this project, `GameManager.cs` uses `PlayerPrefs` to save and load the player’s `score` and `highscore`.

A normal variable only exists while the game is running. For example, if the game only stored the high score in an integer variable, that value would disappear when the game closes. `PlayerPrefs` solves this by storing values with keys such as `"score"` and `"highscore"`.

In this script, the current score and high score are handled differently:

- `score` means the current player score, which can be saved during level completion, but it is reset when the application quits.
- `highScore` means the best score saved on this device, which is saved when the player beats the previous record.

## Code Evidence

The first important part is the `Start()` function:

```csharp
private void Start()
{
    HandleStartUp();
}
```

`Start()` is a Unity event function. Unity calls it once before the first frame update. In this project, `Start()` calls `HandleStartUp()`, which loads saved score data.

```csharp
void HandleStartUp()
{
    if (PlayerPrefs.HasKey("highscore"))
    {
        highScore = PlayerPrefs.GetInt("highscore");
    }
    if (PlayerPrefs.HasKey("score"))
    {
        score = PlayerPrefs.GetInt("score");
    }
    UpdateUIElements();
}
```

This code checks whether saved values already exist before replacing the default values. If `"highscore"` exists, the game loads it with `PlayerPrefs.GetInt("highscore")`. If `"score"` exists, the game loads it with `PlayerPrefs.GetInt("score")`. After that, the UI is updated so the player can see the correct values.

The second important part is the score update logic:

```csharp
public static void AddScore(int scoreAmount)
{
    score += scoreAmount;
    if (score > instance.highScore)
    {
        SaveHighScore();
    }
    UpdateUIElements();
}
```

`AddScore()` increases the current score. Then it checks whether the current score is higher than the saved high score. If the player beats the old record, the script calls `SaveHighScore()`.

```csharp
public static void SaveHighScore()
{
    if (score > instance.highScore)
    {
        PlayerPrefs.SetInt("highscore", score);
        instance.highScore = score;
    }
    UpdateUIElements();
}
```

This is the main code that saves the high score. `PlayerPrefs.SetInt("highscore", score)` stores the current score as the new high score. Then `instance.highScore = score` updates the value inside the current `GameManager`.

The third important part is what happens when the application quits:

```csharp
private void OnApplicationQuit()
{
    SaveHighScore();
    ResetScore();
}
```

`OnApplicationQuit()` is another Unity event function. Unity calls it when the application closes or when Play Mode ends in the editor.

```csharp
public static void ResetScore()
{
    PlayerPrefs.SetInt("score", 0);
    score = 0;
}
```

When the game quits, the script saves the high score first, then resets the current score to 0. This means the best score is preserved, but the current score does not continue forever across separate play sessions.

## Event Chain

The event chain starts when the scene begins:

```text
Unity starts the scene
-> Unity calls GameManager.Start()
-> Start() calls HandleStartUp()
-> HandleStartUp() checks PlayerPrefs.HasKey("highscore")
-> if the key exists, PlayerPrefs.GetInt("highscore") loads the saved high score
-> HandleStartUp() also checks PlayerPrefs.HasKey("score")
-> if the key exists, PlayerPrefs.GetInt("score") loads the saved score
-> UpdateUIElements() refreshes the UI
```

During gameplay, the player can earn score:

```text
Player earns score
-> GameManager.AddScore(scoreAmount) is called
-> score increases
-> GameManager checks if score is greater than highScore
-> if score > highScore, SaveHighScore() is called
-> PlayerPrefs.SetInt("highscore", score) saves the new record
-> UpdateUIElements() refreshes the UI
```

When the application quits:

```text
Unity application quit event
-> Unity calls GameManager.OnApplicationQuit()
-> SaveHighScore() makes sure the best score is saved
-> ResetScore() sets "score" back to 0 in PlayerPrefs
-> the current score is reset, but the high score is kept
```

## Why This Matters

This matters because players expect high scores to remain after closing and reopening a game. If high score only existed as a normal variable, it would reset when the game stopped. `PlayerPrefs` allows the game to remember simple data between play sessions.

This also shows how Unity code is event-driven. The script does not run from a traditional `main()` method. Instead, Unity calls event functions at specific times:

- `Start()` is called when the scene begins, so it is a good place to load saved data.
- `OnApplicationQuit()` is called when the application exits, so it is a good place to save or reset data.
- `AddScore()` is not called directly by Unity, but it can be called by gameplay logic when the player earns points.

Because of this, `PlayerPrefs` is connected to Unity’s event-driven structure. The data is loaded, changed, saved, and reset when Unity or gameplay events happen.

## Improvement Idea

One small improvement would be to call `PlayerPrefs.Save()` after writing important values. Unity usually saves PlayerPrefs automatically, but calling `Save()` makes the save point clearer and forces Unity to write the data immediately.

For example, `SaveHighScore()` could be changed like this:

```csharp
public static void SaveHighScore()
{
    if (score > instance.highScore)
    {
        PlayerPrefs.SetInt("highscore", score);
        PlayerPrefs.Save();
        instance.highScore = score;
    }
    UpdateUIElements();
}
```

Another useful improvement would be to update `ResetScore()` in the same way:

```csharp
public static void ResetScore()
{
    PlayerPrefs.SetInt("score", 0);
    PlayerPrefs.Save();
    score = 0;
}
```

This would make the saving behavior more explicit and easier to understand when reading the code. This improvement would not change the game rules. It only makes the save behavior clearer and more reliable.

## Sources

- [Unity Scripting API: PlayerPrefs](https://docs.unity3d.com/ScriptReference/PlayerPrefs.html)
- [Unity Scripting API: MonoBehaviour.Start](https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html)
- [Unity Scripting API: MonoBehaviour.OnApplicationQuit](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnApplicationQuit.html)
- [Unity Manual: Order of execution for event functions](https://docs.unity3d.com/Manual/execution-order.html)
- `GameManager.cs` from the provided 2D Shooter project

## Reflection

Before this lab, we thought score and high score were just normal variables stored inside `GameManager`. After reading the script, we learned that the game uses `PlayerPrefs` to store values locally between play sessions.

The most important thing we learned is that normal variables and saved values are different. A normal variable only exists while the game is running, but a value saved with `PlayerPrefs` can be loaded again later.

We also learned that Unity scripts are event-driven. `Start()` is used to load saved data when the scene begins, `AddScore()` updates the score during gameplay, and `OnApplicationQuit()` saves the high score and resets the current score when the application exits.
