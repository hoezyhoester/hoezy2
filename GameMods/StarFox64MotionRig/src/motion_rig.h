#ifndef MOTION_RIG_H
#define MOTION_RIG_H

#include "modding.h"
#include "PR/ultratypes.h"

// Handed to the native library (native/sf64_motion_rig.c) once per game frame. Every field is
// 4 bytes and the native side reads them by offset, so keep the two in step.
typedef struct MotionRigInput {
    /* 0x00 */ s32 flying;      // in a level, in control of the craft and not paused
    /* 0x04 */ s32 form;        // PlayerForm, -1 when not flying
    /* 0x08 */ s32 visPerFrame; // game frame length in 60 Hz VIs
    /* 0x0C */ s32 shields;
    /* 0x10 */ s32 boosting;    // 1 boosting, -1 braking, 0 neither
    /* 0x14 */ f32 pos[3];      // game units, as drawn
    /* 0x20 */ f32 fwd[3];      // the craft's nose
    /* 0x2C */ f32 up[3];       // the craft's top
    /* 0x38 */ s32 stickX;      // the player's stick, -80..80
    /* 0x3C */ s32 stickY;
} MotionRigInput; // size = 0x40

// From the native library.
RECOMP_IMPORT(".", void sf64_motion_rig_configure(s32 enabled, const char* host, s32 port, s32 boostShake));
RECOMP_IMPORT(".", void sf64_motion_rig_update(MotionRigInput* input));

#endif
