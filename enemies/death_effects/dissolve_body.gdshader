shader_type spatial;
render_mode cull_disabled;
// Marks every pixel of the body that's actually drawn, for xray_bone to read.
stencil_mode write, compare_always, 1;

// A dead enemy's body chars, flashes and dissolves away, its edge glowing as it goes. Noise is in world
// space so the separate body-part meshes dissolve as one. sweep_weight turns the scatter into a sweep
// along sweep_direction, from sweep_from across sweep_span metres.
instance uniform vec4 base_color : source_color = vec4(1.0);
instance uniform float char_amount = 0.0;
instance uniform float flash = 0.0;
instance uniform float dissolve = 0.0;
instance uniform vec3 sweep_direction = vec3(1.0, 0.0, 0.0);
instance uniform float sweep_from = 0.0;
instance uniform float sweep_span = 1.0;

uniform sampler2D noise : repeat_enable, filter_linear;
uniform float noise_scale = 1.5;
uniform vec4 char_color : source_color = vec4(0.05, 0.04, 0.035, 1.0);
// Glowing flecks in the char, as a multiple of edge_color.
uniform float char_embers = 0.0;
uniform vec4 edge_color : source_color = vec4(1.0, 0.45, 0.05, 1.0);
uniform float edge_width = 0.08;
uniform float edge_glow = 3.0;
uniform vec4 flash_color : source_color = vec4(0.85, 0.75, 1.0, 1.0);
uniform float flash_glow = 4.0;
uniform float sweep_weight = 0.0;

varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float world_noise(vec3 p) {
    float n = texture(noise, p.xy * noise_scale).r + texture(noise, p.zy * noise_scale).r + texture(noise, p.xz * noise_scale).r;
    // The noise bunches in the middle of its range; spread it so dissolve takes the body evenly.
    return clamp((n / 3.0 - 0.3) / 0.4, 0.0, 1.0);
}

void fragment() {
    float n = world_noise(world_position);
    float along = clamp((dot(world_position, sweep_direction) - sweep_from) / sweep_span, 0.0, 1.0);
    float survives = mix(n, along, sweep_weight);

    // Stretched so dissolve 0 leaves everything, edge included, and 1 takes everything.
    float threshold = dissolve * (1.0 + edge_width * 2.0) - edge_width;
    if (survives < threshold) {
        discard;
    }

    float edge = (1.0 - smoothstep(0.0, edge_width, survives - threshold)) * step(0.001, dissolve);
    float embers = pow(world_noise(world_position * 3.7), 6.0) * char_embers * char_amount;

    ALBEDO = mix(mix(base_color.rgb, char_color.rgb, char_amount), flash_color.rgb, flash);
    EMISSION = edge_color.rgb * (edge * edge_glow + embers) + flash_color.rgb * flash * flash_glow;
}
