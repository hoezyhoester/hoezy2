// Motion rig telemetry for SpaceMonkey (https://github.com/PHARTGAMES/SpaceMonkey), the native
// library of the "Motion Rig Telemetry" mod for Star Fox 64: Recompiled.
//
// Once per game frame the mod hands over the player's craft (src/motion_rig.c): where it is
// and which way its nose and top point. From that this works out the g-forces the pilot would feel
// and sends them as "XRTM" version 2 UDP packets, the same format DriveClub Recomp and Xerenge use,
// so SpaceMonkey's Star Fox 64 button reads them like it reads those games.

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>

#ifdef _WIN32
#   ifndef WIN32_LEAN_AND_MEAN
#       define WIN32_LEAN_AND_MEAN
#   endif
#   include <winsock2.h>
#   include <ws2tcpip.h>
    using socket_t = SOCKET;
    static const socket_t invalid_socket = INVALID_SOCKET;
#   define close_socket closesocket
#else
#   include <arpa/inet.h>
#   include <netinet/in.h>
#   include <sys/socket.h>
#   include <unistd.h>
#   include <fcntl.h>
    using socket_t = int;
    static const socket_t invalid_socket = -1;
#   define close_socket close
#endif

#include "recomp.h"

#ifdef _WIN32
#   define DLLEXPORT extern "C" __declspec(dllexport)
#else
#   define DLLEXPORT extern "C" __attribute__((visibility("default")))
#endif

// The mod runtime refuses a native library without this.
extern "C" {
#ifdef _WIN32
    __declspec(dllexport)
#else
    __attribute__((visibility("default")))
#endif
    uint32_t recomp_api_version = 1;
}

namespace {
    // Settings, from the mod's options in the game's mod menu.
    std::mutex settings_mutex;
    bool enabled = true;
    std::string host = "127.0.0.1";
    int port = 20790;
    float boost_shake = 0.5f;   // 0..1, the mod's "Boost shake" option
    std::atomic_bool settings_changed = false; // Nothing is sent until the mod passes its settings.

    constexpr float units_to_metres = 0.06f;  // An Arwing at cruising speed is ~260 km/h.
    constexpr float max_speed = 400.0f;       // m/s; faster than this is a cut, not flight.
    // The game's craft turn far harder than a real one would (a full-stick turn is ~7 g), so the
    // g-forces are scaled down to what a rig can show and capped.
    constexpr float accel_scale = 0.25f;
    constexpr float max_accel = 25.0f;        // m/s^2 per axis (~2.5 g), after scaling.
    constexpr float accel_smoothing = 0.5f;   // Per game frame; the game's movement steps at 30 Hz.
    constexpr uint32_t game_star_fox_64 = 3;

    // Suspension sent per corner (mm, + compressed): the craft's lean from its g-forces, as SpaceMonkey
    // would simulate it, plus a fast rumble while boosting. SpaceMonkey doesn't smooth suspension
    // positions, so the rumble reaches the rig and shakers (its g-forces are smoothed too much to shake).
    constexpr float susp_mm_per_accel = 2.0f;   // mm per m/s^2 (after scaling)
    constexpr float susp_max_mm = 60.0f;
    constexpr float shake_max_mm = 12.0f;        // at "Boost shake" 100%

    struct Vec { float x, y, z; };
    Vec operator+(Vec a, Vec b) { return { a.x + b.x, a.y + b.y, a.z + b.z }; }
    Vec operator-(Vec a, Vec b) { return { a.x - b.x, a.y - b.y, a.z - b.z }; }
    Vec operator*(Vec a, float s) { return { a.x * s, a.y * s, a.z * s }; }
    float dot(Vec a, Vec b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
    float length(Vec a) { return std::sqrt(dot(a, a)); }
    Vec normalize(Vec a) { float l = length(a); return l > 1e-6f ? a * (1.0f / l) : Vec{ 0.0f, 0.0f, 1.0f }; }
    // Same argument order as System.Numerics.Vector3.Cross, which SpaceMonkey uses (right = cross(up, fwd)).
    Vec cross(Vec a, Vec b) { return { a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x }; }

    struct {
        socket_t sock = invalid_socket;
        sockaddr_in dest{};
        bool dest_valid = false;
        uint32_t sequence = 0;
        std::chrono::steady_clock::time_point start = std::chrono::steady_clock::now();

        bool have_pos = false, have_vel = false;
        Vec pos{}, vel{}, accel{};
        float yaw = 0.0f;
        int32_t shields = 0;
        float impact = 0.0f;
        int impact_frames = 0;
        Vec fwd{ 0.0f, 0.0f, 1.0f }, up{ 0.0f, 1.0f, 0.0f };
        float shake_level = 0.0f;   // eases in and out with the boost
        uint32_t shake_frame = 0;
        uint32_t rng = 0x12345678u;
    } state;

    float random_unit() { // 0..1
        state.rng = state.rng * 1664525u + 1013904223u;
        return (state.rng >> 8) * (1.0f / 16777216.0f);
    }

    void reset_motion() {
        state.have_pos = false;
        state.have_vel = false;
        state.accel = {};
        state.impact = 0.0f;
        state.impact_frames = 0;
    }

    void open_socket() {
        if (state.sock != invalid_socket) {
            close_socket(state.sock);
            state.sock = invalid_socket;
        }
        state.dest_valid = false;

        bool cur_enabled;
        std::string cur_host;
        int cur_port;
        {
            std::lock_guard lock{ settings_mutex };
            cur_enabled = enabled;
            cur_host = host;
            cur_port = port;
        }
        if (!cur_enabled) {
            return;
        }

#ifdef _WIN32
        static bool wsa_started = false;
        if (!wsa_started) {
            WSADATA wsa_data;
            if (WSAStartup(MAKEWORD(2, 2), &wsa_data) != 0) {
                return;
            }
            wsa_started = true;
        }
#endif

        std::memset(&state.dest, 0, sizeof(state.dest));
        state.dest.sin_family = AF_INET;
        state.dest.sin_port = htons(static_cast<uint16_t>(cur_port));
        if (inet_pton(AF_INET, cur_host.c_str(), &state.dest.sin_addr) != 1) {
            fprintf(stderr, "Motion rig: \"%s\" is not an IPv4 address, telemetry is off\n", cur_host.c_str());
            return;
        }

        state.sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
        if (state.sock == invalid_socket) {
            return;
        }
#ifdef _WIN32
        u_long nonblocking = 1;
        ioctlsocket(state.sock, FIONBIO, &nonblocking);
#else
        fcntl(state.sock, F_SETFL, fcntl(state.sock, F_GETFL, 0) | O_NONBLOCK);
#endif
        state.dest_valid = true;
        printf("Motion rig: sending telemetry to %s:%d\n", cur_host.c_str(), cur_port);
    }

    template <typename T> void put(uint8_t* buf, size_t offset, T value) {
        std::memcpy(buf + offset, &value, sizeof(T)); // Little-endian on every platform the recomp runs on.
    }

    float read_f32(uint8_t* rdram, gpr addr, int32_t offset) {
        int32_t bits = MEM_W(offset, addr);
        float value;
        std::memcpy(&value, &bits, sizeof(value));
        return std::isfinite(value) ? value : 0.0f;
    }

    float wrap_angle(float a) {
        constexpr float pi = 3.14159265358979f;
        while (a > pi) a -= 2.0f * pi;
        while (a < -pi) a += 2.0f * pi;
        return a;
    }
}

// sf64_motion_rig_configure(s32 enabled, const char* host, s32 port, s32 boost_shake_percent),
// called every few seconds.
DLLEXPORT void sf64_motion_rig_configure(uint8_t* rdram, recomp_context* ctx) {
    bool new_enabled = (int32_t)ctx->r4 != 0;
    gpr host_addr = ctx->r5;
    int new_port = std::clamp((int)(int32_t)ctx->r6, 1, 65535);
    float new_shake = std::clamp((int)(int32_t)ctx->r7, 0, 100) / 100.0f;

    std::string new_host;
    if ((int32_t)host_addr != 0) {
        for (int i = 0; i < 255; i++) {
            char c = (char)MEM_B(i, host_addr);
            if (c == '\0') break;
            if (c != ' ') new_host += c;
        }
    }
    if (new_host.empty()) {
        new_host = "127.0.0.1";
    }

    static bool configured = false;
    std::lock_guard lock{ settings_mutex };
    boost_shake = new_shake;
    if (!configured || new_enabled != enabled || new_host != host || new_port != port) {
        configured = true;
        enabled = new_enabled;
        host = new_host;
        port = new_port;
        settings_changed = true;
    }
}

// sf64_motion_rig_update(MotionRigInput* input), once per game frame.
DLLEXPORT void sf64_motion_rig_update(uint8_t* rdram, recomp_context* ctx) {
    gpr in = ctx->r4;

    if (settings_changed.exchange(false)) {
        open_socket();
    }
    if (!state.dest_valid) {
        return;
    }

    bool flying = MEM_W(0x00, in) != 0;
    int32_t vis_per_frame = std::clamp<int32_t>(MEM_W(0x08, in), 1, 4);
    float dt = vis_per_frame / 60.0f;

    float accel_right = 0.0f, accel_up = 0.0f, accel_fwd = 0.0f;
    float yaw_rate = 0.0f, pitch = 0.0f, roll = 0.0f, speed = 0.0f;

    if (flying) {
        // The game's axes are x right, y up, z backward (the craft flies toward -z); SpaceMonkey's
        // are x right, y up, z forward, so z is flipped.
        Vec pos = { read_f32(rdram, in, 0x14), read_f32(rdram, in, 0x18), -read_f32(rdram, in, 0x1C) };
        pos = pos * units_to_metres;
        Vec fwd = normalize({ read_f32(rdram, in, 0x20), read_f32(rdram, in, 0x24), -read_f32(rdram, in, 0x28) });
        Vec up = normalize({ read_f32(rdram, in, 0x2C), read_f32(rdram, in, 0x30), -read_f32(rdram, in, 0x34) });
        Vec right = normalize(cross(up, fwd));
        up = normalize(cross(fwd, right));
        state.fwd = fwd;
        state.up = up;

        float yaw = std::atan2(fwd.x, fwd.z);
        pitch = std::asin(std::clamp(fwd.y, -1.0f, 1.0f));
        roll = std::atan2(-right.y, up.y);

        if (state.have_pos) {
            Vec vel = (pos - state.pos) * (1.0f / dt);
            if (length(vel) > max_speed) {
                // A cut (warp, restart, checkpoint): start over rather than send a huge jolt.
                reset_motion();
            } else {
                if (state.have_vel) {
                    Vec accel = (vel - state.vel) * (1.0f / dt);
                    state.accel = state.accel + (accel - state.accel) * accel_smoothing;
                }
                state.vel = vel;
                state.have_vel = true;
                speed = length(vel);
                yaw_rate = wrap_angle(yaw - state.yaw) / dt;
            }
        }
        state.pos = pos;
        state.have_pos = true;
        state.yaw = yaw;

        accel_right = std::clamp(dot(state.accel, right) * accel_scale, -max_accel, max_accel);
        accel_up = std::clamp(dot(state.accel, up) * accel_scale, -max_accel, max_accel);
        accel_fwd = std::clamp(dot(state.accel, fwd) * accel_scale, -max_accel, max_accel);

        // Hits: a jolt sized by the shield lost.
        int32_t shields = MEM_W(0x0C, in);
        if (shields < state.shields) {
            state.impact = std::clamp((state.shields - shields) / 24.0f, 0.5f, 3.0f);
            state.impact_frames = 2;
        }
        state.shields = shields;
        if (state.impact_frames > 0) {
            state.impact_frames--;
        } else {
            state.impact = 0.0f;
        }
    } else {
        reset_motion();
        state.shields = 0;
    }

    uint8_t buf[192] = {};
    std::memcpy(buf, "XRTM", 4);
    put<uint16_t>(buf, 4, 2);                               // version
    put<uint16_t>(buf, 6, flying ? (1 | 2 | 4 | 8) : 2);    // in a race, inputs, own accelerations, suspension
    put<uint32_t>(buf, 8, state.sequence++);
    put<uint32_t>(buf, 12, game_star_fox_64);
    put<double>(buf, 16, std::chrono::duration<double>(std::chrono::steady_clock::now() - state.start).count());
    // 24..47: position 0, SpaceMonkey moves the craft along its nose at its speed.
    put<double>(buf, 48, state.fwd.x); put<double>(buf, 56, state.fwd.y); put<double>(buf, 64, state.fwd.z);
    put<double>(buf, 72, state.up.x);  put<double>(buf, 80, state.up.y);  put<double>(buf, 88, state.up.z);
    put<float>(buf, 96, speed);
    float stick_x = flying ? std::clamp(MEM_W(0x38, in) / 80.0f, -1.0f, 1.0f) : 0.0f;
    int32_t boost = flying ? MEM_W(0x10, in) : 0;            // 1 boosting, -1 braking
    put<float>(buf, 100, !flying ? 0.0f : boost > 0 ? 1.0f : boost < 0 ? 0.0f : 0.5f); // throttle
    put<float>(buf, 104, boost < 0 ? 1.0f : 0.0f);            // brake
    put<float>(buf, 108, stick_x);                            // steering: the stick
    put<float>(buf, 112, -1.0f);                              // rpm (none)
    put<float>(buf, 116, -1.0f);                              // gear (none)
    put<float>(buf, 120, -1.0f);                              // clutch (none)
    put<float>(buf, 128, accel_right);
    put<float>(buf, 132, accel_up);
    put<float>(buf, 136, accel_fwd);
    put<float>(buf, 140, yaw_rate);
    put<float>(buf, 144, pitch);
    put<float>(buf, 148, roll);
    put<float>(buf, 152, state.yaw);
    put<float>(buf, 156, state.impact);

    // Suspension: lean from the g-forces, and the boost rumble.
    float target_shake = (flying && boost > 0) ? 1.0f : 0.0f;
    state.shake_level += (target_shake - state.shake_level) * (target_shake > state.shake_level ? 0.5f : 0.25f);
    float shake_strength;
    {
        std::lock_guard lock{ settings_mutex };
        shake_strength = boost_shake;
    }
    float shake_amp = shake_max_mm * shake_strength * state.shake_level;
    state.shake_frame++;
    float heave = accel_up * susp_mm_per_accel;
    float pitch_mm = -accel_fwd * susp_mm_per_accel;   // braking: nose dips
    float roll_mm = -accel_right * susp_mm_per_accel;  // turning right: the left side compresses
    float corner_sign[4][2] = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } }; // FL FR RL RR: front, left
    for (int i = 0; i < 4; i++) {
        float travel = 0.0f;
        if (flying) {
            travel = heave + corner_sign[i][0] * pitch_mm * 0.5f + corner_sign[i][1] * roll_mm * 0.5f;
            if (shake_amp > 0.01f) {
                // Alternates every frame (~15 Hz) with some randomness, each corner a little different.
                float sign = ((state.shake_frame + (i & 1)) & 1) ? 1.0f : -1.0f;
                travel += sign * shake_amp * (0.6f + 0.4f * random_unit());
            }
            travel = std::clamp(travel, -susp_max_mm, susp_max_mm);
        }
        put<float>(buf, 160 + 4 * i, travel);
        put<float>(buf, 176 + 4 * i, 1.0f + travel / 100.0f); // wheel load ratio follows the travel
    }

    sendto(state.sock, reinterpret_cast<const char*>(buf), sizeof(buf), 0,
           reinterpret_cast<const sockaddr*>(&state.dest), sizeof(state.dest));
}
