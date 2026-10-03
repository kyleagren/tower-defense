shader_type spatial;
render_mode world_vertex_coords;

// A Water-killed enemy's body: soaks dark and glossy, then sinks through the ground, cut away below it
// with a bright waterline where it crosses the surface. Driven per body mesh by SinkDeath.
instance uniform vec4 base_color : source_color = vec4(1.0);
instance uniform float wetness = 0.0;
instance uniform float sink_depth = 0.0;
// The surface it sinks through, in world height. Far below anything until the sinking starts.
instance uniform float ground_height = -10000.0;

uniform vec3 soaked_tint : source_color = vec3(0.35, 0.45, 0.6);
uniform float soaked_roughness = 0.15;
uniform vec4 waterline_color : source_color = vec4(0.75, 0.9, 1.0, 1.0);
uniform float waterline_width = 0.05;
uniform float waterline_glow = 0.4;

varying float height_above_ground;

void vertex() {
    VERTEX.y -= sink_depth;
    height_above_ground = VERTEX.y - ground_height;
}

void fragment() {
    if (height_above_ground < 0.0) {
        discard;
    }

    float waterline = 1.0 - smoothstep(0.0, waterline_width, height_above_ground);
    vec3 soaked = mix(base_color.rgb, base_color.rgb * soaked_tint, wetness);

    ALBEDO = mix(soaked, waterline_color.rgb, waterline);
    ROUGHNESS = mix(1.0, soaked_roughness, wetness);
    EMISSION = waterline_color.rgb * waterline * waterline_glow;
}
