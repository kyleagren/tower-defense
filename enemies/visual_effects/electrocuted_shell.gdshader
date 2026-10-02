shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec4 line_color : source_color = vec4(1.0, 0.9, 0.1, 0.9);
// Lines around the body, how thin each is (a share of the gap between lines), how far they wander
// sideways as they climb, and how fast the set crawls around.
uniform float line_count = 6.0;
uniform float line_width : hint_range(0.0, 1.0) = 0.08;
uniform float wobble = 0.25;
uniform float crawl_speed = 1.5;

void fragment() {
    // UV.x runs around the open cylinder, UV.y up it.
    float wander = sin(UV.y * 14.0 + TIME * 11.0) * wobble + sin(UV.y * 31.0 - TIME * 17.0) * wobble * 0.5;
    float band = fract(UV.x * line_count + wander + TIME * crawl_speed);

    if (band > line_width) {
        discard;
    }

    ALBEDO = line_color.rgb;
    ALPHA = line_color.a;
}
