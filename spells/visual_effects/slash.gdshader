shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Set per slash through SetInstanceShaderParameter, so every slash shares one material. The stroke
// shows from the blade's start up to head along its length (UV.x).
instance uniform float head : hint_range(0.0, 1.0) = 1.0;
instance uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform vec4 color : source_color = vec4(0.3, 0.65, 1.0, 0.85);

void fragment() {
    if (head <= 0.0) {
        discard;
    }

    float t = UV.x / head;

    // Widest at the middle of what's showing, narrowing to a point at either end.
    if (t > 1.0 || abs(UV.y * 2.0 - 1.0) > sin(t * PI)) {
        discard;
    }

    ALBEDO = color.rgb;
    ALPHA = color.a * opacity;
}
