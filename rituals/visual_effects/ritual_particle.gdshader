shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

instance uniform vec4 particle_color : source_color = vec4(1.0);

uniform float energy = 2.0;

void vertex() {
    // Billboarded, keeping each particle's own scale.
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
        vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
        vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
        vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
        vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
    float falloff = 1.0 - smoothstep(0.0, 1.0, length(UV * 2.0 - 1.0));
    ALBEDO = mix(particle_color.rgb, vec3(1.0), 0.3) * COLOR.rgb * COLOR.a * falloff * energy;
}
