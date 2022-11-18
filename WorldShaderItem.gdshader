shader_type spatial;

render_mode async_visible,blend_mix,depth_draw_opaque,cull_back,diffuse_lambert,specular_disabled;
uniform vec4 albedo : hint_color;
uniform sampler2DArray texture_albedo : hint_albedo;
uniform float specular;
uniform float metallic;
uniform float alpha_scissor_threshold;
uniform float roughness : hint_range(0,1);

void vertex() {
    MODELVIEW_MATRIX = INV_CAMERA_MATRIX * mat4(CAMERA_MATRIX[0],CAMERA_MATRIX[1],CAMERA_MATRIX[2],WORLD_MATRIX[3]);
}

void fragment() {
    float tile_size = 16.0;
    vec2 uv_adj = UV;//fract(UV);
    //uv_adj.x = clamp(uv_adj.x, 0.5/tile_size, 1.0 - 0.5/tile_size);
    //uv_adj.y = clamp(uv_adj.y, 0.5/tile_size, 1.0 - 0.5/tile_size);
    //uv_adj = uv_adj*0.98 + vec2(0.01);
    vec2 base_uv = uv_adj;//(uv_adj + UV2) / vec2(16.0, 8.0);
    vec4 albedo_tex = texture(texture_albedo, vec3(base_uv, UV2.x + UV2.y*16.0));
    ALBEDO = albedo.rgb * albedo_tex.rgb;
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    float alpha = albedo.a * albedo_tex.a;
    if(alpha < alpha_scissor_threshold)
        discard;
    //ALPHA_SCISSOR = alpha_scissor_threshold;
}
