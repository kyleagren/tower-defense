shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled;

instance uniform vec4 core_color : source_color = vec4(1.0);
// 0 is an even glow, 1 is churning plasma.
instance uniform float turbulence = 0.3;
instance uniform float flow_speed = 0.5;
// Toward the next cast; the core brightens as it fills.
instance uniform float charge = 1.0;
// 1 alive, 0 broken.
instance uniform float vitality = 1.0;

uniform float rim_power = 2.5;
uniform float energy = 2.0;
uniform vec3 broken_color : source_color = vec3(0.12);

varying vec3 local_position;

void vertex() {
    local_position = VERTEX;
}

float hash(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
        mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y),
        f.z);
}

float fbm(vec3 p) {
    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 4; i++) {
        sum += noise(p) * amplitude;
        p = p * 2.03 + vec3(1.7, 9.2, 3.1);
        amplitude *= 0.5;
    }
    return sum;
}

void fragment() {
    float time = TIME * flow_speed;
    float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    float rim = pow(1.0 - facing, rim_power);

    float plasma = fbm(local_position * 4.0 + vec3(0.0, -time * 2.0, time));
    float body = mix(0.55, plasma * 1.4, turbulence);

    vec3 hot = mix(core_color.rgb, vec3(1.0), 0.6) * pow(facing, 6.0) * 0.8;
    float brightness = mix(0.45, 1.0, charge);
    vec3 alive = (core_color.rgb * (body * 0.6 + rim * 1.2) + hot) * energy * brightness;
    vec3 dead = broken_color * (0.4 + rim);

    ALBEDO = mix(dead, alive, vitality);
}
