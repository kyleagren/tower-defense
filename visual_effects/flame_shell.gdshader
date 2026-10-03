shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// Flames rising up an open cylinder: a burning enemy's sheath, and Immolate's eruption column. The mesh
// is a unit cylinder scaled into place, so its local height runs -0.5 to 0.5.
uniform sampler2D noise : repeat_enable, filter_linear;
uniform vec4 edge_color : source_color = vec4(0.9, 0.2, 0.02, 1.0);
uniform vec4 core_color : source_color = vec4(1.0, 0.7, 0.2, 1.0);
uniform float strength = 1.2;
uniform float rise_speed = 1.4;
// How many tongues fit around the cylinder - whole numbers keep the seam invisible.
uniform float tongues = 3.0;
// How high the flames reach before thinning out completely, as a share of the shell.
uniform float reach = 0.8;

// 0 burns fully; rising to 1 pulls the flames down to nothing.
instance uniform float fade : hint_range(0.0, 1.0) = 0.0;

varying float height;

void vertex() {
    height = VERTEX.y + 0.5;
}

void fragment() {
    vec2 uv = vec2(UV.x * tongues, height * 0.9 - TIME * rise_speed);
    float n = texture(noise, uv).r;
    float detail = texture(noise, uv * vec2(2.0, 1.6) + vec2(0.3, -TIME * rise_speed * 0.8)).r;
    float flame = n * 0.6 + detail * 0.5;

    // Tongues need more noise to survive the higher they climb, so they taper to points.
    float climb = height / max(reach * (1.0 - fade), 0.001);
    float threshold = mix(0.3, 0.75, climb);
    float body = smoothstep(threshold, threshold + 0.08, flame) * smoothstep(0.0, 0.06, height);

    if (body <= 0.0 || climb > 1.0) {
        discard;
    }

    float core = smoothstep(threshold + 0.06, threshold + 0.25, flame) * (1.0 - climb);

    ALBEDO = mix(edge_color.rgb, core_color.rgb, core) * strength;
    ALPHA = body * (1.0 - fade) * (1.0 - climb * 0.5);
}
