# Overview

Alien Invaders: The Tower Defense is a mixed reality game for Meta Quest 3 built with Unity and C#. The player starts in their real room, rides a closed elevator to the top of a tower and defends it from alien ships that come out of portals in the sky. Every enemy shot is announced by a red aiming laser, so losing always comes down to reflexes and never to surprise. When the tower finally collapses, an emergency elevator brings the player back to their room to see the score and the local Top 10.

My goal with this project is to grow as a software engineer by building a complete game around a core written in plain C#. All the gameplay rules (tower integrity, vector based tilt, waves, weapon heat, scoring and records) live in classes that have no dependency on Unity and are covered by unit tests. The Unity scripts only connect those rules to the headset, the controllers and the visuals. This keeps the logic easy to test and easy to explain, and it leaves room for future hardware such as a motion platform with actuators.

C# features shown in the code:

* Variables, expressions, conditionals and loops in the tower, wave and scoring rules
* Classes and structs (`WaveConfig`, `MotionSample`, `MatchResult`)
* Inheritance with an `abstract` base class: `EnemyShip` is extended by `Fighter` and `Bomber`
* A C style union emulated with `[StructLayout(LayoutKind.Explicit)]` and `[FieldOffset]` in `PackedRecordCode`
* Reading and writing files: the Top 10 is saved as JSON and a match can be recorded to a motion file
* Events (`event Action<...>`) that let the core notify Unity without knowing about it

Current status: the core rules and their 41 unit tests are complete, and the base scene already runs on the headset with passthrough. Gameplay features are being added step by step (see Future Work).

Software Demo Video: the link will be added here once the walkthrough is recorded.

# Development Environment

* Unity 6.3 LTS (6000.3.27f1) with the Universal Render Pipeline
* Meta XR All in One SDK v207 with OpenXR, targeting Meta Quest 3 (Android, IL2CPP, ARM64)
* Visual Studio Code on macOS (Apple Silicon)
* Meta Quest Developer Hub and Android Debug Bridge (adb) to install and debug builds on the headset
* Unity Test Framework (NUnit) for the EditMode tests

Language and libraries:

* C# 9, the language version supported by Unity
* `System.Numerics` vectors in the core, so it never references `UnityEngine`
* `JsonUtility` for the records file
* Meta XR building blocks for the camera rig and passthrough

Project layout:

```
Assets/_Project/Scripts/Core/     game rules in plain C# (no UnityEngine)
Assets/_Project/Scripts/Game/     MonoBehaviours that connect the core to Unity
Assets/_Project/Tests/EditMode/   NUnit tests for the core
Assets/_Project/Scenes/Main.unity the only scene (AR and VR live in the same scene)
```

How to run:

1. Open the project with Unity 6000.3.27f1 and Android Build Support installed.
2. Add these free Unity Asset Store packages with Window > Package Manager > My Assets. Their license does not allow publishing them in this repository. Without them the game still runs, but the buildings near the tower are plain boxes, the streets are painted on the ground, the portals and explosions keep their simple shapes and the ships, the gun and the sky have no art.
   * Simple Building Pack (Polyeler)
   * Starfighter Set 01 (PolyAnarchy)
   * Midgard Skybox Prime (LaFinca): the game uses its panorama 13 Rose Cumulus Ocean Wide Cloud Garden 4K, imported as a 2D texture
   * Urban Road Pack Lite Clean and Weathered (Carthage Studio): also import the URP.unitypackage found inside it
   * FREE Low Poly Sci Fi Energy Blasters Demo Cosmic Retro (The PolynestLab)
   * Magic Effects FREE (Hovl Studio): convert its materials to URP with Window > Rendering > Render Pipeline Converter (Material Upgrade)
3. Turn on developer mode on the Quest 3 and connect it to the computer with a USB cable.
4. Go to File > Build Profiles > Android, choose the Quest 3 as Run Device and press Build And Run.
5. To run the tests, open Window > General > Test Runner and run all EditMode tests.

# Useful Websites

* [Unity Manual](https://docs.unity3d.com/Manual/index.html)
* [Meta Horizon OS documentation for Unity](https://developers.meta.com/horizon/documentation/unity/)
* [C# documentation](https://learn.microsoft.com/dotnet/csharp/)
* [StructLayoutAttribute reference](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.structlayoutattribute)
* [System.Numerics.Vector3 reference](https://learn.microsoft.com/dotnet/api/system.numerics.vector3)
* [NUnit documentation](https://docs.nunit.org/)

# Future Work

* Platform calibration and the AR to VR transition inside the closed elevator
* Energy weapon with overheating, haptics and a cyan laser
* Enemy ships (Fighter and Bomber) with object pooling and aiming lasers
* Tower tilt, staged drops and shake driven by the motion bus
* Portals and endless waves with short breaks between them
* Wrist radar and spatial audio
* Critical state, emergency rescue elevator and results panels
* Local Top 10 with arcade style initials and a shareable record code
* Final art, skybox, effects and sound
* Performance pass to stay at 72 FPS or more on Quest 3
* Optional comfort mode with reduced tilt
* Motion platform with actuators controlled over UDP by an ESP32
* Online leaderboard and a daily challenge with a shared seed
