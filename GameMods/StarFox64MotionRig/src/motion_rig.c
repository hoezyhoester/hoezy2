// Motion rig telemetry for Star Fox 64: Recompiled.
//
// Once per game frame the player's craft (Arwing, Landmaster or Blue-Marine) is handed to the
// native library, which works out the g-forces and sends them as UDP packets for SpaceMonkey.
//
// The orientation is built the way the game draws the craft (Display_Player_Update), minus the
// visual-only extras (damage shake, bobbing, barrel rolls and the loop's pitch), so a barrel roll
// doesn't spin the rig and a loop doesn't flip it. The loop and the hits still come through as
// g-forces.

#include "modding.h"
#include "recompconfig.h"
#include "sys.h"
#include "sf64player.h"
#include "context.h"
#include "functions.h"
#include "variables.h"
#include "motion_rig.h"

static MotionRigInput sMotionRigInput;
static s32 sConfigTimer = 0;

static bool MotionRig_IsFlying(Player* player) {
    if ((gGameState != GSTATE_PLAY) || (gPlayState != PLAY_UPDATE) || gVersusMode || (gPlayer == NULL)) {
        return false;
    }
    if ((player->form != FORM_ARWING) && (player->form != FORM_LANDMASTER) && (player->form != FORM_BLUE_MARINE)) {
        return false;
    }
    switch (player->state) {
        case PLAYERSTATE_ACTIVE:
        case PLAYERSTATE_U_TURN:
        case PLAYERSTATE_START_360:
        case PLAYERSTATE_LEVEL_INTRO:
        case PLAYERSTATE_ENTER_WARP_ZONE:
        case PLAYERSTATE_ANDROSS_MOUTH:
            return true;
        default:
            return false;
    }
}

// The settings from the mod menu, read again every few seconds so changes apply while playing.
static void MotionRig_Configure(void) {
    char* host;

    if (sConfigTimer > 0) {
        sConfigTimer--;
        return;
    }
    sConfigTimer = 90;

    host = recomp_get_config_string("host");
    sf64_motion_rig_configure(recomp_get_config_u32("enabled") == 0, host, (s32) recomp_get_config_double("port"),
                              (s32) recomp_get_config_double("boost_shake"));
    recomp_free_config_string(host);
}

RECOMP_HOOK_RETURN("Display_Update") void MotionRig_OnDisplayUpdate(void) {
    MotionRigInput* in = &sMotionRigInput;
    Player* player = gPlayer;
    Vec3f localFwd = { 0.0f, 0.0f, 1.0f };
    Vec3f localUp = { 0.0f, 1.0f, 0.0f };
    Vec3f fwd;
    Vec3f up;
    f32 yaw;

    // In two-player mode the game draws twice a frame; versus isn't sent anyway.
    if (gVersusMode) {
        return;
    }

    MotionRig_Configure();

    in->flying = MotionRig_IsFlying(player);
    in->visPerFrame = gVIsPerFrame;

    if (!in->flying) {
        in->form = -1;
        sf64_motion_rig_update(in);
        return;
    }

    in->form = player->form;
    in->shields = player->shields;
    // 1 boosting, -1 braking, 0 neither. The Arwing and Blue-Marine boost and brake through their boost
    // speed (above 0 boosting, below 0 braking); only the Landmaster sets boostActive, for either.
    if (player->form == FORM_LANDMASTER) {
        in->boosting = !player->boostActive ? 0 : (gInputHold->button & gBrakeButton[player->num]) ? -1 : 1;
    } else {
        in->boosting = (player->boostSpeed > 0.1f) ? 1 : (player->boostSpeed < -0.1f) ? -1 : 0;
    }
    in->stickX = gInputPress->stick_x;
    in->stickY = gInputPress->stick_y;

    // The same rotations as the game's own draw of the craft.
    yaw = (player->yRot_114 + player->rot.y + 180.0f) * M_DTOR;
    Matrix_Push(&gCalcMatrix);
    if (player->form == FORM_LANDMASTER) {
        Matrix_RotateX(gCalcMatrix, player->rot.x * M_DTOR, MTXF_NEW);
        Matrix_RotateZ(gCalcMatrix, (player->rot.z + player->rockAngle) * M_DTOR, MTXF_APPLY);
        Matrix_RotateY(gCalcMatrix, yaw, MTXF_APPLY);
        Matrix_RotateZ(gCalcMatrix, -player->zRotBank * M_DTOR, MTXF_APPLY);
    } else {
        Matrix_RotateY(gCalcMatrix, yaw, MTXF_NEW);
        Matrix_RotateX(gCalcMatrix, -(player->xRot_120 + player->rot.x) * M_DTOR, MTXF_APPLY);
        Matrix_RotateZ(gCalcMatrix, -(player->rot.z + player->zRotBank + player->rockAngle) * M_DTOR, MTXF_APPLY);
    }
    Matrix_MultVec3fNoTranslate(gCalcMatrix, &localFwd, &fwd);
    Matrix_MultVec3fNoTranslate(gCalcMatrix, &localUp, &up);
    Matrix_Pop(&gCalcMatrix);

    in->pos[0] = player->pos.x;
    in->pos[1] = player->pos.y;
    in->pos[2] = player->trueZpos + player->zPath;
    in->fwd[0] = fwd.x;
    in->fwd[1] = fwd.y;
    in->fwd[2] = fwd.z;
    in->up[0] = up.x;
    in->up[1] = up.y;
    in->up[2] = up.z;

    sf64_motion_rig_update(in);
}
