shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

// Immolate's eruption on the ground: a ragged ring of fire racing out from the middle to the edge, with
// a scorch left behind it that fades on its own clock. The disc is a unit cylinder's top scaled out to
// the eruption's radius.
uniform sampler2D noise : repeat_enable, filter_linear;
uniform vec4 fire_color : source_color = vec4(1.0, 0.45, 0.06, 1.0);
uniform vec4 hot_color : source_color = vec4(1.0, 0.92, 0.6, 1.0);
uniform vec4 scorch_color : source_color = vec4(0.05, 0.025, 0.015, 0.7);
uniform float fire_strength = 2.0;
// How deep the band of fire behind the front is, as a share of the radius.
uniform float flame_depth = 0.3;
uniform float raggedness = 0.3;

// How far the front has spread: 0 at the middle, 1 at the edge, and past 1 the fire has burned out.
instance uniform float wave = 0.0;
// 0 while the scorch is fresh, 1 once it's gone.
instance uniform float scorch_fade = 0.0;

varying vec2 local_xz;

void vertex() {
    local_xz = VERTEX.xz;
}

void fragment() {
    float n = texture(noise, local_xz * 0.8 + vec2(TIME * 0.3, 0.0)).r;
    float r = length(local_xz) + (n - 0.5) * raggedness;

    if (r > 1.0 || r > wave) {
        discard;
    }

    // Brightest at the front, dying away behind it, and flickering with the noise.
    float behind = wave - r;
    float fire = (1.0 - smoothstep(0.0, flame_depth, behind)) * (0.6 + n * 0.8);
    fire *= 1.0 - smoothstep(1.0, 1.3, wave);
    fire = clamp(fire, 0.0, 1.0);

    float scorch = scorch_color.a * (1.0 - scorch_fade) * smoothstep(1.0, 0.75, r);
    vec3 flame = mix(fire_color.rgb, hot_color.rgb, smoothstep(0.5, 1.0, fire)) * fire_strength;

    ALBEDO = mix(scorch_color.rgb, flame, fire);
    ALPHA = max(scorch, fire);
}
