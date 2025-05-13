# Code Samples

This is a repo of various code samples from several different projects, mostly in Unity with C#.

## Kinematic Character Controller (Unity/C#)
This character controller is my go-to controller for most of my projects, it is kinematic and thus driven by its own logic rather than relying on Unity's rigidbody physics system.
It is designed to work with "parent" objects such as moving platforms or vehicles while still maintaining snappy controls for the player, akin to games like Quake or Team Fortress.

## NPC Behaviour (Unity/C#)
An excerpt from a personal project in which I am creating a "social stealth" game, where a player engages with patrolling guards and NPCs that behave realistically to environmental stimuli (sight, sound, dialogue).
These NPCs have an evidence system that allows them to remember important clues and stimuli and react appropriately; as an example, a character can observe an out-of-place broken object and react with a dialogue scene by checking for nearby allies, or perform a different scene alone. The NPC can also react differently to the stimulus later if it has already interacted with it once, allowing characters to recall events from prior gameplay moments.
This project is incomplete and is partly made with a visual scripting system (gamecreator.io)

## Sköll Dialogue Box System (Unity/C#)
Samples for both a sprite-based and 3D geometry based dialogue box system to display ornate text in line with the aesthetics of the project they were made for. This code mainly governs the animation of lettering as it fades in and out, and the resizing of the dialogue box to fit text according to parameters.

## Weapon Implementation for TF2Classic (Source/C++)
An example of one of the unique weapons I created for [TF2: Classic](https://tf2classic.com) in C++ and Valve's proprietary engine. This one had a lot of unique features that hadn't been done before in the game, so we couldn't implement this with our usual attribute system - most weapons we add are extensions of existing basic types (shotguns, melee weapons, projectile launchers), but this grenade launcher fires healing grenades and generates charge towards a special projectile that sticks to surfaces and creates a bubble of invulnerability.
This code is also networked for a fast-paced 24-player environment with lag compensation, allowing players to compete from different continents without interruption or fault.
