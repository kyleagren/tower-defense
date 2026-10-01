shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec4 wind_color : source_color = vec4(0.55, 0.92, 0.59, 0.45);
// The stripes' color before the sweep picks anything up.
uniform vec4 stripe_color : source_color = vec4(0.22, 0.58, 0.3, 0.7);
// How many stripes wind around the cone, how much they twist per metre of height, and how fast
// they climb.
uniform float stripe_count = 2.0;
uniform float twist = 0.6;
uniform float spin = 1.5;
uniform float stripe_width : hint_range(0.0, 1.0) = 0.35;
// How tightly the stripes on the flat top curl inward, per unit of radius.
uniform float top_twist = 1.2;
// How many stripes curl in on the flat top, apart from the sides' stripe_count.
uniform float top_stripe_count = 1.0;

// Set per sweep through SetInstanceShaderParameter once it picks up an element.
instance uniform float infused = 0.0;
instance uniform vec4 infusion_color : source_color = vec4(1.0);

varying vec3 local_position;
varying float top_face;

void vertex() {
    local_position = VERTEX;
    // The flat top faces straight up; the cone's sides lean outward and down.
    top_face = NORMAL.y > 0.99 ? 1.0 : 0.0;
}

void fragment() {
    float angle = atan(local_position.x, local_position.z) / TAU;
    // The sides climb with height; the top curls in toward its center instead.
    bool on_top = top_face > 0.5;
    float along = on_top ? length(local_position.xz) * top_twist : local_position.y * twist;
    float count = on_top ? top_stripe_count : stripe_count;
    float band = fract((angle + along - TIME * spin) * count);
    vec4 stripe = infused > 0.5 ? vec4(infusion_color.rgb, stripe_color.a) : stripe_color;
    vec4 color = band < stripe_width ? stripe : wind_color;
    ALBEDO = color.rgb;
    ALPHA = color.a;
}
