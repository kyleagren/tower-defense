shader_type spatial;
render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Rings spreading over a dark wet patch where a Water-killed body sinks. Driven by SinkDeath; age is
// seconds since the body started going under.
instance uniform float age = 0.0;
instance uniform float fade = 1.0;

uniform vec4 ring_color : source_color = vec4(0.75, 0.9, 1.0, 0.8);
uniform vec4 pool_color : source_color = vec4(0.05, 0.12, 0.25, 0.5);
uniform float ring_interval = 0.45;
uniform float ring_lifetime = 1.2;
uniform float ring_width = 0.04;
uniform float pool_radius = 0.6;
uniform float pool_grow_time = 0.6;

void fragment() {
    float r = length(UV * 2.0 - 1.0);

    // Newest ring first. Each starts at the centre and fades out as it reaches the edge.
    float rings = 0.0;
    for (int i = 0; i < 4; i++) {
        float ring_age = mod(age, ring_interval) + float(i) * ring_interval;
        float t = ring_age / ring_lifetime;
        if (ring_age > age || t > 1.0) {
            break;
        }
        float band = 1.0 - smoothstep(0.0, ring_width, abs(r - t));
        rings = max(rings, band * (1.0 - t));
    }

    float pool = (1.0 - smoothstep(pool_radius * 0.8, pool_radius, r)) * clamp(age / pool_grow_time, 0.0, 1.0);

    ALBEDO = mix(pool_color.rgb, ring_color.rgb, rings);
    ALPHA = max(pool * pool_color.a, rings * ring_color.a) * fade;
}
