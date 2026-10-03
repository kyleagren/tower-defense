shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

// Converge's and Repel's footprint: spiral arms turning in toward the center for Converge (direction 1)
// or out from it for Repel (direction -1), over a disc that darkens or brightens toward the middle. The
// disc is a unit cylinder's top scaled out to the field's radius.
uniform vec4 field_color : source_color = vec4(0.2, 0.25, 0.75, 0.25);
uniform vec4 arm_color : source_color = vec4(0.55, 0.6, 1.0, 0.8);
uniform vec4 center_color : source_color = vec4(0.0, 0.0, 0.0, 0.85);
uniform float direction = 1.0;
uniform float arms = 4.0;
uniform float twist = 3.0;
uniform float speed = 0.5;
// Above 0, a ring sweeps out from the center once every this many seconds - Repel's pulses.
uniform float pulse_interval = 0.0;
uniform vec4 pulse_color : source_color = vec4(1.0, 0.9, 0.6, 0.9);

varying vec2 local_xz;

void vertex() {
    local_xz = VERTEX.xz;
}

void fragment() {
    float r = length(local_xz);

    if (r > 1.0) {
        discard;
    }

    float angle = atan(local_xz.y, local_xz.x) / TAU;
    // Lines of constant phase are spirals; moving the phase with time slides them in or out.
    float phase = angle * arms + r * twist + TIME * speed * direction;
    float band = fract(phase);
    float arm = smoothstep(0.0, 0.12, band) * smoothstep(0.45, 0.15, band);
    // Arms fade in from the rim and out before the middle, where the center color takes over.
    arm *= smoothstep(1.0, 0.85, r) * smoothstep(0.08, 0.35, r);

    float center = 1.0 - smoothstep(0.0, 0.45, r);
    vec4 color = mix(field_color, center_color, center);
    color = mix(color, arm_color, arm * arm_color.a);

    if (pulse_interval > 0.0) {
        float front = fract(TIME / pulse_interval);
        float ring = smoothstep(0.08, 0.0, abs(r - front)) * (1.0 - front);
        color = mix(color, pulse_color, ring * pulse_color.a);
    }

    // A soft rim, so the disc doesn't end on a hard edge.
    color.a *= smoothstep(1.0, 0.95, r);

    ALBEDO = color.rgb;
    ALPHA = color.a;
}
