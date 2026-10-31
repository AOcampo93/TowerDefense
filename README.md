# Overview

Alien Invaders: The Tower Defense is a mixed reality game for Meta Quest 3 built with Unity and C#. The player starts in their real room, steps into a closed elevator and rides it to the top of a tower that stands 200 meters above a city at dusk. Alien ships come out of portals in the sky, raid the city and then turn on the tower. The player defends it with a blaster in the right hand. Every enemy shot is announced by a red aiming laser, so losing always comes down to reflexes and quick decisions and never to surprise. The tower leans toward the side that takes the most damage, gives way floor by floor and finally collapses. An emergency elevator then brings the player back to their real room, where a scoreboard shows the score and the local Top 10.

My goal with this project was to grow as a software engineer by building a complete game around a core written in plain C#. All the gameplay rules (tower integrity, vector based tilt, waves, ships, weapon heat, power ups, scoring and records) live in classes that have no dependency on Unity and are covered by unit tests. The Unity scripts only connect those rules to the headset, the controllers, the visuals and the sound. This keeps the logic easy to test and easy to explain, and it leaves room for future hardware such as a motion platform with actuators.

Software Demo Video: [watch the walkthrough on YouTube](https://youtu.be/vTTPaDXaoOI)

Meta Horizon Store: the game is currently in review by Meta. I will share the store link here as soon as it is approved.

## How a match plays

The whole game is one scene, and the `GameController` moves it through a state machine:

1. **Calibration (real room).** A start screen on a scoreboard explains the game. The player holds the right controller against their chest and pulls the trigger, so the virtual platform lines up with where they stand and where they look.
2. **Ascent.** The elevator doors close, the real room fades into the virtual world and the elevator rises to the top of the tower. The controllers vibrate as each floor passes.
3. **Arrival.** The elevator opens on a small platform with a railing, and the blaster appears in the right hand.
4. **Defense and Respite.** Waves come one after another with only a couple of seconds of rest between them. Portals open, ships burst out, raid the city and then a few of them attack the tower.
5. **Critical.** When the tower drops to 33% integrity, alarms sound and the tower shakes all the time, but the player can still fight.
6. **Rescue.** When integrity reaches 0, the tower collapses and an emergency elevator drops the player back down.
7. **Results (real room).** The elevator opens in the player's room. A "TOWER DOWN" scoreboard shows the score, the waves survived, the accuracy and the Top 10. A new record asks for arcade style initials.

## Gameplay rules

* **The tower.** It has 100 points of integrity, but damage is stored for each of its four sides. The tilt is the vector sum of the damage on each side, so hits on opposite sides cancel out and the tower leans toward its weakest side. It can lean up to 7, 13 or 20 degrees depending on the damage stage. At 66% and at 33% a floor gives way and the whole tower drops a few meters.
* **Fighters.** Fast and fragile (5 hits). They often fly to their station in two quick dashes that form a wide V, so they are hard to lead. They get faster every wave.
* **Bombers.** Slow and armored (12 hits), from wave 2. They hit the tower three times harder than a fighter and drop a heavy bomb on the city during each raid, a blast the player sees first, then hears and feels as a jolt of the platform.
* **Fair warning.** A ship that attacks the tower stops at its station and shows a red aiming laser first. Destroying it before it fires prevents the damage. The warning gets shorter every wave, but never shorter than 0.6 seconds.
* **Waves.** The first wave has 6 ships and every wave adds 2 more. Only a few ships attack the tower at the same time, and that number grows slowly, so the pressure rises without becoming unfair.
* **The blaster.** It fires 10 bolts per second while the trigger is held. Bolts are instant beams with a slight aim assist. Holding the trigger too long overheats the gun and locks it for a moment.
* **Power ups.** From wave 5, some bombers leave glowing green cubes when they explode. A few bolts collect them, and for 8 seconds the gun becomes a super weapon with a thick green beam that does three times the damage.
* **Score.** `score = seconds survived × 10 + ships destroyed × 5 + accuracy × 1000`. Time survived matters most; kills and accuracy break ties.

## Controls

* **Calibrate the platform:** hold the right controller against your chest and pull the right trigger.
* **Shoot:** right trigger. Hold it for a burst.
* **See the radar:** look at the watch on your left wrist. Red dots are ships and purple dots are portals.
* **End or restart the match:** hold fire on END NOW or RESTART on the wrist console.
* **Enter initials:** right thumbstick up and down to change the letter, left and right to move, trigger to confirm.
* **Buttons on the scoreboard:** point with the right controller and pull the trigger.

## C# features shown in the code

* **Variables and expressions:** gameplay numbers in `Core/Balance.cs` and the score formula in `ScoreSystem.Calculate`.
* **Conditionals:** the `if` checks in `TowerIntegrity.ReceiveDamage` and the `switch` over game states in `GameController.EnterState`.
* **Loops:** a `for` that adds the damage of every side in `TowerIntegrity.Integrity`, and a `while` that fires every threshold crossed by one heavy hit.
* **Functions:** methods with parameters and return values across the core, such as `TowerIntegrity.SideFromAzimuth`.
* **Classes:** every rule is its own class: `TowerIntegrity`, `WeaponHeat`, `WaveGenerator`, `ScoreSystem` and `HighScoreTable`.
* **Structs:** `WaveConfig`, `MotionSample` and `MatchResult` in `Core/Structs.cs`, value types that are copied instead of shared.
* **Union:** `PackedRecordCode` emulates a C style union with `[StructLayout(LayoutKind.Explicit)]` and `[FieldOffset]`. The score, the seconds, the wave and a checksum share the same 8 bytes as one 64 bit record code that players can share.
* **Stretch challenge, inheritance:** `abstract class EnemyShip` declares the `abstract` method `PickTurningPoint`. `Fighter` and `Bomber` extend it, call `base(...)` and `override` that method, and `ShipFactory` creates the right one.
* **Stretch challenge, files:** `HighScoreTable` reads and writes the Top 10 in `records.json`. It writes a temporary file first and then swaps it in, and uses `try` and `catch` so a broken file never crashes the game. `MotionRecorder` also saves every match as a motion track on a worker thread.
* **Events:** `event Action<...>` fields such as `OnFire`, `OnThresholdReached` and `OnStateChanged` let the core notify Unity without knowing about it.
* **Interfaces and generics:** `IMotionOutput`, `IShootable`, `IRecordSerializer` and the generic `ObjectPool<T> where T : Component`.

## Architecture

The code is split into four layers. Each one only knows about the layers below it.

* **Core** (26 scripts): the game rules in plain C#. Its assembly definition has `noEngineReferences: true`, so the compiler itself stops it from using Unity. It uses `System.Numerics` vectors instead of `UnityEngine` ones. Every gameplay number lives in `Balance.cs`.
* **Game** (17 scripts): the MonoBehaviours that act as the bridge. `GameController` creates the core objects in `Awake`, subscribes to their events and advances them every frame in `Update`. Other scripts read the controllers, aim the blaster, spawn ships and portals, and send play stats.
* **Motion** (3 scripts): outputs of the `MotionBus`. The core publishes a `MotionSample` (tilt, drop and shake) 20 times per second, and every `IMotionOutput` applies it: `CameraMotionOutput` leans the world, `HapticMotionOutput` vibrates the controllers and `PlatformMotionOutput` is a placeholder for a future motion platform.
* **Views** (36 scripts): presentation only. Ships, explosions, the elevator, the city, the sky, the radar, the scoreboard, the wrist console, the music and the sounds.

How one hit travels through the layers: a ship in the core finishes aiming and raises `OnFire`. Its `ShipController` hears it and tells the `ShipSpawner`, which calls `TowerIntegrity.ReceiveDamage` with the side of the tower that was hit. The core updates the damage and raises `OnDamaged` and, if a threshold was crossed, `OnThresholdReached`. `GameController` reacts by dropping the tower or entering the critical state, and the views play the sounds and the shake. The core never knows that Unity exists.

Other design choices:

* **Object pooling.** Ships, explosions and effects are created once and reused, so the game never calls `Instantiate` or `Destroy` during a match.
* **Hitscan shooting.** Each bolt is a `Physics.SphereCast` from the tip of the barrel. Anything that implements `IShootable` can be shot: ships, power ups and the buttons of the wrist console.
* **Seeded waves.** `WaveGenerator` takes a seed, so a run can be repeated exactly.

Project layout:

```
Assets/_Project/Scripts/Core/      game rules in plain C# (no UnityEngine)
Assets/_Project/Scripts/Game/      MonoBehaviours that connect the core to Unity
Assets/_Project/Scripts/Motion/    outputs of the motion bus (world, haptics, platform)
Assets/_Project/Scripts/Views/     presentation: models, effects, UI and sound
Assets/_Project/Tests/EditMode/    NUnit tests for the core
Assets/_Project/Scenes/Main.unity  the only scene (the real room and the virtual world share it)
Assets/_Project/Shaders/           custom shaders (screen fade, body silhouette, sky)
Assets/Plugins/Android/            custom manifest and Gradle template for the store build
```

## Comfort

The player never moves artificially. The platform and the railing always stay still, and the world around them is what leans, drops and shakes. The tilt speed is limited to 5 degrees per second, every drop is announced a moment before it happens, and the hard landing of the rescue elevator hides the switch back to the real room behind a quick blink. The player's own body is shown as a faint glow (using the Quest 3 body tracking), so they always have a stable point of reference.

## Tests

The core is covered by 78 EditMode tests written with NUnit: tower integrity and tilt, thresholds, weapon heat, waves, ship behavior, the motion bus, scoring, the Top 10 file, the record code, the wrist console hold and the play stats queue. Because the core does not depend on Unity, these tests run in seconds without starting the game or wearing the headset.

## Play stats and feedback

To learn how a free game is received, the game sends anonymous play stats to my own web API: session and match length, the wave reached, the average frame rate and which pages were opened. The player is identified only by a random id created on the first launch, and nothing personal is sent. The results scoreboard also has a feedback page with a star rating, topics to improve and a message typed on a keyboard drawn on the board. Events wait in a queue saved on the headset, so a match played offline is sent later. Sessions played in the Unity Editor are not sent.

# Development Environment

Tools:

* Unity 6.3 LTS (6000.3.27f1) with the Universal Render Pipeline
* Meta XR All in One SDK v207 with OpenXR, targeting Meta Quest 3 and 3S (Android, IL2CPP, ARM64)
* Unity Input System for the controllers and the haptics
* Visual Studio Code on macOS (Apple Silicon)
* Meta XR Simulator to test in the editor, and Meta Quest Developer Hub with Android Debug Bridge (adb) to install and debug builds on the headset
* Unity Test Framework (NUnit) for the EditMode tests
* Blender to reduce and adjust 3D models for the headset
* Git with Git LFS for the models, images and sounds

Language and libraries:

* C# 9, the language version supported by Unity
* `System.Numerics` vectors in the core, so it never references `UnityEngine`
* `System.IO` and `JsonUtility` for the records file and the motion track
* `UnityWebRequest` for the play stats and the feedback
* Meta XR building blocks for the camera rig, passthrough and body tracking

## How to run

You need a Meta Quest 3 or 3S with developer mode turned on, a USB cable, and Unity 6000.3.27f1 with Android Build Support.

1. Clone the repository and open it with Unity 6000.3.27f1.
2. Add these free Unity Asset Store packages with Window > Package Manager > My Assets. Their license does not allow publishing them in this repository. Without them the game still runs, but the buildings near the tower are plain boxes, the streets are painted on the ground, the portals and explosions keep their simple shapes, the ships, the gun and the sky have no art and there is no music.
   * Simple Building Pack (Polyeler)
   * Starfighter Set 01 (PolyAnarchy)
   * Midgard Skybox Prime (LaFinca): the game uses its panorama 13 Rose Cumulus Ocean Wide Cloud Garden 4K, imported as a 2D texture
   * Urban Road Pack Lite Clean and Weathered (Carthage Studio): also import the URP.unitypackage found inside it
   * FREE Low Poly Sci Fi Energy Blasters Demo Cosmic Retro (The PolynestLab)
   * Magic Effects FREE (Hovl Studio): convert its materials to URP with Window > Rendering > Render Pipeline Converter (Material Upgrade)
   * Synthwave Music Volume I (AD Sounds): the game plays The Different, Final, Cold Water, Winds and Fly Above during the ride up and the fight
3. To try it in the editor, activate the Meta XR Simulator and press Play.
4. To play it on the headset, connect the Quest 3 with the USB cable, go to File > Build Profiles > Android, choose the Quest as Run Device and press Build And Run. Unity signs development builds with its own debug key.
5. To run the tests, open Window > General > Test Runner and run all EditMode tests.

## Store build

The game has been submitted for a free release on the Meta Horizon Store and is in review by Meta. The store link will be added at the top of this file once it is approved. The store build uses its own package id (`com.arturoocampo.alieninvaders`) and a release key that is kept outside the repository. A custom launcher manifest removes the permissions the game does not use and limits the build to Quest 3 and 3S, and a custom Gradle template leaves out the 32 bit libraries that the Meta SDK includes, because the store rejects them.

## Credits

* Ships: Starfighter Set 01 (PolyAnarchy)
* Buildings: Simple Building Pack (Polyeler)
* Streets: Urban Road Pack (Carthage Studio)
* Blaster: Cosmic Retro (The PolynestLab)
* Effects: Magic Effects FREE (Hovl Studio)
* Sky: Midgard Skybox (LaFinca) and "The Sky Is On Fire" from Poly Haven (CC0)
* Music: Synthwave Music Vol. I (AD Sounds)
* Sounds: Kenney and OpenGameArt (CC0)
* Textures: ambientCG (CC0)
* Command console and scoreboard: generated with Meshy AI from my own reference images

Each folder with third party files has a `CREDITS.md` with the details.

# Useful Websites

* [Unity Manual](https://docs.unity3d.com/Manual/index.html)
* [Meta Horizon OS documentation for Unity](https://developers.meta.com/horizon/documentation/unity/)
* [C# documentation](https://learn.microsoft.com/dotnet/csharp/)
* [StructLayoutAttribute reference](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.structlayoutattribute)
* [System.Numerics.Vector3 reference](https://learn.microsoft.com/dotnet/api/system.numerics.vector3)
* [NUnit documentation](https://docs.nunit.org/)
* [Unity Asset Store](https://assetstore.unity.com/)

# Future Work

* Public release on the Meta Horizon Store once Meta approves the game
* Motion platform with two or three actuators controlled over UDP by an ESP32, plugged in through `PlatformMotionOutput` without changing any game rule
* Replay a recorded motion track on the platform without wearing the headset
* Online leaderboard that receives each `MatchResult` from the game
* Daily challenge where every player gets the same waves from a shared seed
* Optional comfort mode with reduced tilt and shake
* Portals that open on the real walls of the player's room before the elevator ride
* New enemies (the `Mothership` ship type is already reserved), more weapons and boss waves
* More performance headroom on the GPU, which runs close to its limit during the biggest waves
