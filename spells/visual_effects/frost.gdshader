shader_type spatial;
render_mode unshaded, depth_draw_never, shadows_disabled;

// Frost veins along the noise's midline, over a faint icy tint, with the odd sparkle.
uniform sampler2D noise : repeat_enable;
uniform vec4 base_color : source_color = vec4(0.55, 0.8, 1.0, 0.18);
uniform vec4 frost_color : source_color = vec4(0.8, 0.93, 1.0, 0.6);
uniform float vein_width = 0.04;
// Pattern repeats per this many meters.
uniform float scale = 4.0;

varying vec2 world_xz;

void vertex() {
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    vec2 uv = world_xz / scale;
    float n = texture(noise, uv).r;
    float vein = 1.0 - smoothstep(0.0, vein_width, abs(n - 0.5));
    float sparkle = step(0.985, texture(noise, uv * 8.0 + vec2(TIME * 0.05)).r);
    vec4 color = mix(base_color, frost_color, vein);

    ALBEDO = color.rgb + sparkle;
    ALPHA = max(color.a, sparkle);
}
