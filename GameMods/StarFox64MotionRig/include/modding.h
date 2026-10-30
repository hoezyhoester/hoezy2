#ifndef __MODDING_H__
#define __MODDING_H__

// Do not edit these defines. They use special section names that the recomp mod tool recognizes for specific modding functionality.

#ifdef __cplusplus
#   define EXTERNC extern "C"
#else
#   define EXTERNC
#endif

// The RECOMP_IMPORT has the following attributes:
//   noinline: Prevent import function definitions from being inlined, allowing the mod tool to find relocs. 
//   weak: Allow import definitions to be in headers without triggering multiple definition errors.
#define RECOMP_IMPORT(mod, func) \
    _Pragma("GCC diagnostic push") \
    _Pragma("GCC diagnostic ignored \"-Wunused-parameter\"") \
    _Pragma("GCC diagnostic ignored \"-Wreturn-type\"") \
    EXTERNC __attribute__((noinline, weak, used, section(".recomp_import." mod))) func {} \
    _Pragma("GCC diagnostic pop")

#define RECOMP_EXPORT EXTERNC __attribute__((retain, section(".recomp_export")))

#define RECOMP_PATCH EXTERNC __attribute__((retain, section(".recomp_patch")))

#define RECOMP_FORCE_PATCH EXTERNC __attribute__((retain, section(".recomp_force_patch")))

#define RECOMP_DECLARE_EVENT(func) \
    _Pragma("GCC diagnostic push") \
    _Pragma("GCC diagnostic ignored \"-Wunused-parameter\"") \
    EXTERNC __attribute__((retain, noinline, weak, used, section(".recomp_event"))) void func {} \
    _Pragma("GCC diagnostic pop")

#define RECOMP_CALLBACK(mod, event) __attribute__((retain, section(".recomp_callback." mod ":" #event)))

#define RECOMP_HOOK(func) __attribute__((retain, section(".recomp_hook." func)))

#define RECOMP_HOOK_RETURN(func) __attribute__((retain, section(".recomp_hook_return." func)))

#include "rt64_extended_gbi.h"

#ifndef gEXFillRectangle
#define gEXFillRectangle(cmd, lorigin, rorigin, ulx, uly, lrx, lry)                         \
    G_EX_COMMAND2(cmd, PARAM(RT64_EXTENDED_OPCODE, 8, 24) | PARAM(G_EX_FILLRECT_V1, 24, 0), \
                  PARAM(lorigin, 12, 0) | PARAM(rorigin, 12, 12),                           \
                                                                                            \
                  PARAM((ulx) * 4, 16, 16) | PARAM((uly) * 4, 16, 0),                       \
                  PARAM((lrx) * 4, 16, 16) | PARAM((lry) * 4, 16, 0))
#endif

#define gEXMatrixGroupNoInterpolation(cmd, push, proj, edit)                                                           \
    gEXMatrixGroup(cmd, G_EX_ID_IGNORE, G_EX_INTERPOLATE_SIMPLE, push, proj, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, \
                   G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP,                 \
                   G_EX_COMPONENT_SKIP, G_EX_ORDER_LINEAR, edit, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupInterpolateOnlyTiles(cmd, push, proj, edit)                                                      \
    gEXMatrixGroup(cmd, G_EX_ID_IGNORE, G_EX_INTERPOLATE_SIMPLE, push, proj, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, \
                   G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP,                 \
                   G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_LINEAR, edit, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupDecomposedNormal(cmd, id, push, proj, edit)                                                \
    gEXMatrixGroupDecomposed(cmd, id, push, proj, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE,        \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_LINEAR, edit,           \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupDecomposedNormal2(cmd, id, push, proj, edit)                                               \
    gEXMatrixGroupDecomposed(cmd, id, push, proj, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_AUTO,               \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_LINEAR, edit,           \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupDecomposedSkipRot(cmd, id, push, proj, edit)                                               \
    gEXMatrixGroupDecomposed(cmd, id, push, proj, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_SKIP,               \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_LINEAR, edit,           \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupDecomposedSkipPosRot(cmd, id, push, proj, edit)                                            \
    gEXMatrixGroupDecomposed(cmd, id, push, proj, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP,                      \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_LINEAR, edit,           \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupDecomposedSkipAll(cmd, id, push, proj, edit)                                               \
    gEXMatrixGroupDecomposed(cmd, id, push, proj, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP,                      \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_LINEAR, edit, G_EX_COMPONENT_SKIP,           \
                             G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupDecomposedVerts(cmd, id, push, proj, edit)                                                 \
    gEXMatrixGroupDecomposed(cmd, id, push, proj, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE,        \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_LINEAR, edit,    \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#define gEXMatrixGroupDecomposedVertsOrderAuto(cmd, id, push, proj, edit)                                        \
    gEXMatrixGroupDecomposed(cmd, id, push, proj, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE,        \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, \
                             G_EX_COMPONENT_INTERPOLATE, G_EX_COMPONENT_INTERPOLATE, G_EX_ORDER_AUTO, edit,      \
                             G_EX_COMPONENT_SKIP, G_EX_COMPONENT_SKIP)

#endif