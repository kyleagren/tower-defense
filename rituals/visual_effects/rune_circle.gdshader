shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

instance uniform vec4 circle_color : source_color = vec4(1.0);
// Radians, advanced from C# so a broken Ritual's circle coasts to a stop.
instance uniform float rotation = 0.0;
instance uniform float flicker = 0.0;
instance uniform float glyph_count = 12.0;
instance uniform float vitality = 1.0;

uniform float energy = 1.2;
uniform float line_width = 0.012;
uniform vec3 broken_color : source_color = vec3(0.15);

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float ring(float r, float radius) {
    return 1.0 - smoothstep(0.0, line_width, abs(r - radius));
}

float segment_distance(vec2 p, vec2 a, vec2 b) {
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-4), 0.0, 1.0);
    return length(pa - ba * h);
}

// A made-up rune: three strokes between points on a 3x3 grid, picked by hash, so every cell differs
// but the shapes still read as writing.
float glyph(vec2 cell_uv, float seed) {
    float d = 1.0;
    for (int i = 0; i < 3; i++) {
        float h1 = hash(vec2(seed, float(i)));
        float h2 = hash(vec2(seed, float(i) + 7.3));
        vec2 a = floor(vec2(h1 * 3.0, fract(h1 * 9.7) * 3.0)) * 0.5;
        vec2 b = floor(vec2(h2 * 3.0, fract(h2 * 9.7) * 3.0)) * 0.5;
        d = min(d, segment_distance(cell_uv, a, b));
    }
    return 1.0 - smoothstep(0.04, 0.1, d);
}

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0) {
        discard;
    }

    float angle = atan(p.y, p.x);
    float outer_angle = fract((angle + rotation) / TAU);
    float inner_angle = fract((angle - rotation * 1.6) / TAU);

    float lines = ring(r, 0.96) + ring(r, 0.66) + ring(r, 0.38);

    float glyphs = 0.0;
    float band_inner = 0.71;
    float band_outer = 0.91;
    if (r > band_inner && r < band_outer) {
        float cell = floor(outer_angle * glyph_count);
        float along = fract(outer_angle * glyph_count);
        // A gap either side of each glyph.
        vec2 cell_uv = vec2((along - 0.2) / 0.6, (r - band_inner) / (band_outer - band_inner));
        if (cell_uv.x >= 0.0 && cell_uv.x <= 1.0) {
            glyphs = glyph(cell_uv, cell);
        }
    }

    float dash_band = 1.0 - smoothstep(0.02, 0.02 + line_width, abs(r - 0.52));
    float dashes = dash_band * step(fract(inner_angle * glyph_count * 2.0), 0.4);

    float flick = mix(1.0, hash(vec2(floor(TIME * 14.0), 1.0)) * 0.6 + 0.4, flicker);
    float mask = clamp(lines + glyphs + dashes, 0.0, 1.0);
    vec3 color = mix(broken_color, circle_color.rgb * energy * flick, vitality);

    ALBEDO = color * mask;
}
