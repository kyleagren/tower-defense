shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Wind Blade's trailing streaks. Each lies along its particle's velocity (the particle's local Y) and
// turns to face the camera around that line only. Full at the end nearest the blade, narrowing to a
// point and fading out toward the far end; the whole streak fades over its life (the color ramp).
uniform vec4 wind_color : source_color = vec4(0.55, 0.92, 0.59, 0.5);

// Set per blade through SetInstanceShaderParameter once it picks up an element.
instance uniform float infused = 0.0;
instance uniform vec4 infusion_color : source_color = vec4(1.0);

void vertex() {
    vec3 along = MODEL_MATRIX[1].xyz;
    vec3 origin = MODEL_MATRIX[3].xyz;
    vec3 to_camera = normalize(CAMERA_POSITION_WORLD - origin);
    vec3 axis = normalize(along);
    vec3 side = normalize(cross(axis, to_camera));
    vec3 facing = cross(side, axis);

    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        vec4(side * length(MODEL_MATRIX[0].xyz), 0.0),
        vec4(along, 0.0),
        vec4(facing * length(MODEL_MATRIX[2].xyz), 0.0),
        vec4(origin, 1.0));
}

void fragment() {
    // UV.y runs from the far end (0) to the end nearest the blade (1). Flip to 1.0 - UV.y if the
    // streaks fade the wrong way.
    float nearness = UV.y;

    if (abs(UV.x - 0.5) * 2.0 > nearness) {
        discard;
    }

    vec3 color = infused > 0.5 ? mix(wind_color.rgb, infusion_color.rgb, 0.7) : wind_color.rgb;
    ALBEDO = color;
    ALPHA = wind_color.a * COLOR.a * nearness;
}
