shader_type spatial;
render_mode unshaded;

uniform sampler2D tex : source_color;
uniform float tile_count = 64.0;

void fragment() {
	ALBEDO = texture(tex, UV * tile_count).rgb;
}
