shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Short streaks falling down the cylinder's walls, each column at its own speed and offset - hail or
// rain, depending on how it's set.
uniform vec4 streak_color : source_color = vec4(0.85, 0.95, 1.0, 0.8);
uniform float columns = 40.0;
// Flip the sign if the streaks fall upward.
uniform float fall_speed = 2.5;
uniform float streak_length = 0.12;
// How much of each column a streak fills, side to side.
uniform float streak_width = 0.12;

float hash(float x) {
    return fract(sin(x * 91.3458) * 47453.5453);
}

void fragment() {
    float column = floor(UV.x * columns);
    float speed = fall_speed * (0.8 + 0.4 * hash(column + 13.0));
    float y = fract(UV.y - TIME * speed + hash(column));
    float across = fract(UV.x * columns);

    if (y > streak_length || abs(across - 0.5) > streak_width) {
        discard;
    }

    ALBEDO = streak_color.rgb;
    ALPHA = streak_color.a * (1.0 - y / streak_length);
}
