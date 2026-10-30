# Motion Rig Telemetry for Starfox 64: Recompiled

A mod for [Starfox 64: Recompiled](https://github.com/sonicdcer/Starfox64Recomp) that sends the player's
Arwing, Landmaster or Blue-Marine to SpaceMonkey's **Star Fox 64** button, as UDP packets in the same
"XRTM" version 2 format that DriveClub Recomp uses (game id 3).

- `src/motion_rig.c` runs in the game (hooked after `Display_Update`, once per game frame). It builds the
  craft's nose and top directions the way the game draws the craft, without damage shake, bobbing,
  barrel rolls or the loop's pitch, and hands them over with the position, shields, boost and stick.
- `native/sf64_motion_rig.cpp` is the mod's native library (`sf64_motion_rig.dll` / `.so`). It works out
  the g-forces from the craft's movement (scaled down to what a rig can show, capped at 2.5 g), adds a jolt
  sized by the shields lost in a hit, and sends a packet each frame. In menus, cutscenes, on the pause
  screen and in versus mode it sends "not flying", so SpaceMonkey eases the rig back to rest.

The mod's options (in the game's mod menu): telemetry on/off, the IP address to send to (127.0.0.1 by
default) and the UDP port (20790 by default).

## Building

Set up the two folders the template uses, at the commits Starfox64Recomp uses:

    git clone https://github.com/sonicdcer/sf64 sf64                      # decomp headers
    git clone https://github.com/sonicdcer/Starfox64RecompSyms Starfox64RecompSyms

Then, with clang and ld.lld:

    make
    RecompModTool mod.toml build                                           # from N64Recomp

and the native library (here cross-compiled for Windows; `recomp.h` is in N64Recomp's `include`):

    x86_64-w64-mingw32-g++ -std=c++20 -O2 -shared -static -static-libgcc -static-libstdc++ \
        -I <N64Recomp>/include native/sf64_motion_rig.cpp -o build/sf64_motion_rig.dll -lws2_32

Zip `build/sf64_motion_rig_telemetry.nrm` with `build/sf64_motion_rig.dll` (and `.so` for Linux) at the
top level of the zip; dragging that zip onto the game window installs both.
