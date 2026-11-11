shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

// Alpha scales the whole circle, so a translucent preview color draws a fainter footprint.
instance uniform vec4 circle_color : source_color = vec4(1.0);
// Radians, advanced from C# so a broken Ritual's circle coasts to a stop.
instance uniform float rotation = 0.0;
instance uniform float flicker = 0.0;
instance uniform float glyph_count = 12.0;
instance uniform float vitality = 1.0;
// The outer ring: how far it has filled toward the next cast, and whether that cast is ready.
instance uniform float fill = 1.0;
instance uniform float ready = 1.0;

uniform float energy = 1.2;
uniform float line_width = 0.012;
uniform vec3 broken_color : source_color = vec3(0.15);
// The filled arc's brightness while the next cast is still coming.
uniform float recovering_brightness = 0.45;
uniform float unlit_brightness = 0.12;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float ring(float r, float radius) {
    return 1.0 - smoothstep(0.0, line_width, abs(r - radius));
}

float band(float r, float inner, float outer) {
    return smoothstep(inner - line_width, inner, r) * (1.0 - smoothstep(outer, outer + line_width, r));
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

    float lines = ring(r, 0.82) + ring(r, 0.58) + ring(r, 0.34);

    float glyphs = 0.0;
    float glyph_inner = 0.62;
    float glyph_outer = 0.78;
    if (r > glyph_inner && r < glyph_outer) {
        float cell = floor(outer_angle * glyph_count);
        float along = fract(outer_angle * glyph_count);
        // A gap either side of each glyph.
        vec2 cell_uv = vec2((along - 0.2) / 0.6, (r - glyph_inner) / (glyph_outer - glyph_inner));
        if (cell_uv.x >= 0.0 && cell_uv.x <= 1.0) {
            glyphs = glyph(cell_uv, cell);
        }
    }

    float dash_band = 1.0 - smoothstep(0.02, 0.02 + line_width, abs(r - 0.46));
    float dashes = dash_band * step(fract(inner_angle * glyph_count * 2.0), 0.4);

    float flick = mix(1.0, hash(vec2(floor(TIME * 14.0), 1.0)) * 0.6 + 0.4, flicker);
    float rune_mask = clamp(lines + glyphs + dashes, 0.0, 1.0);
    vec3 runes = circle_color.rgb * energy * flick * rune_mask;

    // Fixed in place rather than turning with the runes: 0 at -Z, running clockwise seen from above.
    float readiness_mask = band(r, 0.87, 0.98);
    float fill_angle = fract(atan(p.x, -p.y) / TAU);
    float lit = step(fill_angle, fill);
    vec3 lit_color = circle_color.rgb * energy * mix(recovering_brightness, 1.0, ready);
    vec3 readiness = mix(circle_color.rgb * unlit_brightness, lit_color, lit) * readiness_mask;

    vec3 dead = broken_color * (rune_mask + readiness_mask);
    ALBEDO = mix(dead, runes + readiness, vitality) * circle_color.a;
}
