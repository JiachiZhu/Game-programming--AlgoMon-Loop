# Change Log — 2D Game Improvement Assignment

## Project
"Project 2D" — a top-down 2D shoot-em-up assembled from the class 2D Game Starter Pack
(`2D_game_assets.unitypackage`).

## Note about progress
An earlier version of this project was lost because the scene was not saved properly
(已与老师口头说明). The current submission was rebuilt during this class session by
re-assembling the starter pack's prefabs and adding the gameplay change described below.

## What is from the starter pack (NOT my work)
Almost everything in this project comes from the class starter pack. I only assembled it
into a playable scene. Specifically, the following are package assets and I did not write
or create them:

- All art (player sprite, enemies, projectiles, backgrounds, planets, asteroids, UI sprites, fonts).
- All audio (music tracks, SFX for fire / hit / explode / game over / menu / pause).
- All gameplay scripts: `Controller.cs` (player movement and aim), `ShootingController.cs`
  (firing), `Projectile.cs`, `Damage.cs`, `Health.cs`, `Enemy.cs`, `EnemySpawner.cs`,
  `GameManager.cs`, `UIManager.cs`, `ScoreDisplay.cs`, `HighScoreDisplay.cs`,
  `LevelLoadButton.cs`, `QuitGameButton.cs`, `CursorChanger.cs`, `TimedObjectDestroyer.cs`,
  and others.
- All prefabs: `Player_Projectile`, the enemy prefabs (StraightShooter, DiagonalShooter,
  Chasers, etc.), `MainMenu`, `GameOverScreen`, `EnemyHitEffect`, `EnemyProjectileHit`,
  `GameManager`, `UIManager`, asteroid border walls, and so on.

## What I actually changed

### 1. Gameplay change — difficulty scales with score (越打人越多)
This is the only meaningful gameplay rule I wrote myself. I added a small block to the
package's `EnemySpawner.cs` so that **the more enemies you kill, the faster new enemies
spawn**:

- Two new Inspector fields: `Min Spawn Delay` (floor) and `Delay Reduction Per 100 Score`.
- Each frame, the spawner reads the current score from `GameManager.score` and computes a
  shorter spawn delay than the configured base value, capped at `Min Spawn Delay`.
- Effect: the longer the player survives, the more pressure they face. The game stops
  feeling static and starts feeling like an escalating challenge.

### 2. Small helper changes (assembly support, not gameplay)
- Added `HealthDisplay.cs` (about 10 lines): a tiny script that reads the Player's
  `Health.currentHealth` each frame and shows it in a TMP text. The package did not have
  one for HP, only for Score and HighScore.
- Added a `useBoundaries` clamp at the bottom of `Controller.cs` (about 10 lines) so the
  plane cannot fly off the playable area. Without this, `Controller.cs` moves the player
  by setting `transform.position` directly and ignores colliders.

## What I tested
- Menu loads, New Game enters the gameplay scene.
- Shooting, enemies, score, hit/death particles, and SFX all work.
- Difficulty scaling is visible: by the time the score is in the hundreds, enemies arrive
  noticeably faster than at the start.
- Game Over screen appears on death and returns to the main menu.

## Credits
- **Everything except the items in "What I actually changed"** is from the class 2D Game
  Starter Pack (`2D_game_assets.unitypackage`).
- **My additions**: the difficulty-scaling code in `EnemySpawner.cs`, the boundary clamp
  in `Controller.cs`, and the `HealthDisplay.cs` script.
