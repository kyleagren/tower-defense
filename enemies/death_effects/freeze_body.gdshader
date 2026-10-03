shader_type spatial;

// A dead enemy frozen solid: ice over its own color, frost bright where it turns from the camera,
// cracks spreading across it, and finally crumbling to powder. Cells and noise are in world space, so
// cracks and crumbling run across the separate body-part meshes as one.
instance uniform vec4 base_color : source_color = vec4(1.0);
instance uniform float frost = 0.0;
instance uniform float crack = 0.0;
instance uniform float dissolve = 0.0;

uniform vec4 ice_color : source_color = vec4(0.65, 0.85, 1.0, 1.0);
uniform float ice_cover = 0.75;
uniform vec4 rim_color : source_color = vec4(0.9, 0.97, 1.0, 1.0);
uniform float rim_glow = 1.5;
uniform vec4 crack_color : source_color = vec4(0.95, 1.0, 1.0, 1.0);
uniform float crack_glow = 3.0;
uniform float cell_size = 0.12;
uniform float crack_width = 0.012;

uniform sampler2D noise : repeat_enable, filter_linear;
uniform float noise_scale = 4.0;
uniform vec4 powder_color : source_color = vec4(0.92, 0.97, 1.0, 1.0);
uniform float powder_width = 0.1;

varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

vec3 hash3(vec3 p) {
    p = vec3(dot(p, vec3(127.1, 311.7, 74.7)), dot(p, vec3(269.5, 183.3, 246.1)), dot(p, vec3(113.5, 271.9, 124.6)));
    return fract(sin(p) * 43758.5453);
}

// x: roughly how far to the nearest cell border. y: a random per cell, deciding when its crack shows.
vec2 cell_border(vec3 p) {
    vec3 cell = floor(p);
    vec3 local = fract(p);
    float nearest = 8.0;
    float second = 8.0;
    vec3 nearest_cell = cell;

    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            for (int z = -1; z <= 1; z++) {
                vec3 offset = vec3(float(x), float(y), float(z));
                float distance_to = length(offset + hash3(cell + offset) - local);

                if (distance_to < nearest) {
                    second = nearest;
                    nearest = distance_to;
                    nearest_cell = cell + offset;
                } else if (distance_to < second) {
                    second = distance_to;
                }
            }
        }
    }

    return vec2(second - nearest, hash3(nearest_cell).x);
}

float world_noise(vec3 p) {
    float n = texture(noise, p.xy * noise_scale).r + texture(noise, p.zy * noise_scale).r + texture(noise, p.xz * noise_scale).r;
    // The noise bunches in the middle of its range; spread it so dissolve takes the body evenly.
    return clamp((n / 3.0 - 0.3) / 0.4, 0.0, 1.0);
}

void fragment() {
    // Stretched so dissolve 0 leaves everything, edge included, and 1 takes everything.
    float threshold = dissolve * (1.0 + powder_width * 2.0) - powder_width;
    float n = world_noise(world_position);
    if (n < threshold) {
        discard;
    }

    float powder = (1.0 - smoothstep(0.0, powder_width, n - threshold)) * step(0.001, dissolve);
    vec2 border = cell_border(world_position / cell_size);
    float line = (1.0 - smoothstep(0.0, crack_width / cell_size, border.x)) * step(border.y, crack);
    float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);

    vec3 iced = mix(mix(base_color.rgb, ice_color.rgb, ice_cover * frost), crack_color.rgb, line);
    ALBEDO = mix(iced, powder_color.rgb, powder);
    ROUGHNESS = mix(1.0, 0.1, frost);
    SPECULAR = mix(0.5, 0.9, frost);
    EMISSION = rim_color.rgb * rim * rim_glow * frost + crack_color.rgb * line * crack_glow;
}
