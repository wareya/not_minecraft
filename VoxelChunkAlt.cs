using Microsoft.CSharp;
using System;
using Godot;
using GDDictionary = Godot.Collections.Dictionary;

using System.Collections.Generic;
using ArrayList = System.Collections.ArrayList;
using GDObject = Godot.Object;

/*
// TODO
class ValueNoise
{
    System.UInt64 Seed = 0;
    int Octaves = 7;
    int Period = 64;
    float Persistence = 1.0f;
    float Lacunarity = 1.5f;
    
    double Noise3D(double x, double y, double z)
    {
        var x0 = Math.Floor(x) % Period;
        var y0 = Math.Floor(y) % Period;
        var z0 = Math.Floor(z) % Period;
        
        return 0.0;
    }
    
    float smoothstep(float t)
    {
        if(t < 0.0f) return 0.0f;
        if(t > 1.0f) return 1.0f;
        return t*t*3.0f - t*t*t*2.0f;
    }

    float Lerp(float t, float a, float b)
    {
        return a + t * (b - a);
    }
}
*/

class EntInfo
{
    public bool is_block = false;
    public Vector3 size = new Vector3(1, 1, 1);
    public Vector3 collision_size = new Vector3(1, 1, 1);
    public Vector2 sprite_coord = new Vector2();
    
    public EntInfo(bool _is_block, Vector3 _size, Vector3 _collision_size, Vector2 _sprite_coord)
    {
        is_block = _is_block;
        size = _size;
        collision_size = _collision_size;
        sprite_coord = _sprite_coord;
    }
}
public class VoxelChunkAlt : Spatial
{
    Node world;
    Vector3 position;
    bool objects_generated = false;
    Dictionary<int, EntInfo> entinfos = new Dictionary<int, EntInfo> {
        {1, new EntInfo(false, new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0.8f, 0.50f, 0.8f), new Vector2(12, 1))},
        {2, new EntInfo(false, new Vector3(0.7f, 1.0f, 0.7f), new Vector3(0.5f, 0.75f, 0.5f), new Vector2(12, 7))},
    };
    int size;
    int size_half;
    AABB bounds;
    byte[] base_data;
    byte[] data;
    byte[] water;
    MeshInstance meshinstance;
    StaticBody collider;
    Dictionary<Vector3, int> ents;
    StaticBody ent_collider;
    MeshInstance ent_meshinstance;
    MeshInstance water_meshinstance;
    int id = 0;
    static Material mat = null;
    static Material ent_mat = null;
    static Material water_mat = null;
    static Material water_mat2 = null;
    OpenSimplexNoise noise = null;
    
    
        
    GDDictionary foreign_infos;
    Dictionary<byte, Vector2> infos_top = new Dictionary<byte, Vector2>();
    Dictionary<byte, Vector2> infos_bottom = new Dictionary<byte, Vector2>();
    Dictionary<byte, Vector2> infos_side = new Dictionary<byte, Vector2>();
    Dictionary<byte, bool> infos_transparent = new Dictionary<byte, bool>();
    
    void InitInfos()
    {
        foreign_infos = (GDDictionary)world.Get("tile_type_info");
        foreach(var key in foreign_infos.Keys)
        {
            var obj = foreign_infos[key];
            var bytekey = (byte)(int)key;
            var tile_info = (GDObject)obj;
            infos_top        [bytekey] = (Vector2)tile_info.Get("coord_top");
            infos_bottom     [bytekey] = (Vector2)tile_info.Get("coord_bottom");
            infos_side       [bytekey] = (Vector2)tile_info.Get("coord_side");
            infos_transparent[bytekey] =    (bool)tile_info.Get("is_transparent");
        }
    }
    
    
    public VoxelChunkAlt()
    {
        
    }
    public VoxelChunkAlt(int world_seed, Node _world, int _size, Vector3 coord)
    {
        position = coord;
        world = _world;
        size = _size;
        size_half = size/2;
        bounds = new AABB(new Vector3(), new Vector3(size-1, size-1, size-1));
        id = (int)(GD.Randi());
        System.Array.Resize(ref data, size*size*size);
        System.Array.Resize(ref base_data, size*size*size);
        System.Array.Resize(ref water, size*size*size);
        for(int i = 0; i < data.Length; i++)
        {
            data[i] = 0;
            base_data[i] = 0;
            water[i] = 0;
        }
        
        ents = new Dictionary<Vector3, int>();
        
        meshinstance = new MeshInstance();
        meshinstance.Mesh = new ArrayMesh();
        AddChild(meshinstance);
        
        ent_meshinstance = new MeshInstance();
        ent_meshinstance.Mesh = new ArrayMesh();
        AddChild(ent_meshinstance);
        
        water_meshinstance = new MeshInstance();
        water_meshinstance.Mesh = new ArrayMesh();
        AddChild(water_meshinstance);
        
        if(coord.y < 0)
        {
            meshinstance.SetLayerMaskBit(0, false);
            meshinstance.SetLayerMaskBit(5, true);
            ent_meshinstance.SetLayerMaskBit(0, false);
            ent_meshinstance.SetLayerMaskBit(5, true);
        }
        
        collider = new StaticBody();
        ent_collider = new StaticBody();

        collider.CreateShapeOwner(collider);
        AddChild(collider);
        
        ent_collider.CreateShapeOwner(collider);
        ent_collider.SetCollisionLayerBit(0, false);
        ent_collider.SetCollisionLayerBit(5, true);
        AddChild(ent_collider);
        
        // TODO move semitransparent blocks like leaves to a separate mesh (the ent mesh?...nah need the sampling shader)
        if(mat == null)
        {
            mat = GD.Load<Material>("res://WorldShaderMat.tres");
            //mat = new SpatialMaterial();
            //mat.ParamsDiffuseMode = SpatialMaterial.DiffuseMode.Lambert;
            //mat.AlbedoTexture = GD.Load<Texture>("res://art/0-ufeff_tiles.png");
            //mat.ParamsUseAlphaScissor = true;
            //mat.ParamsAlphaScissorThreshold = 0.25f;
        }
        if(ent_mat == null)
        {
            var _ent_mat = new SpatialMaterial();
            
            _ent_mat.ParamsDiffuseMode = SpatialMaterial.DiffuseMode.Lambert;
            _ent_mat.AlbedoTexture = GD.Load<Texture>("res://art/0-ufeff_tiles.png");
            _ent_mat.ParamsUseAlphaScissor = true;
            _ent_mat.ParamsAlphaScissorThreshold = 0.25f;
            _ent_mat.ParamsSpecularMode = SpatialMaterial.SpecularMode.Disabled;
            
            _ent_mat.ParamsCullMode = SpatialMaterial.CullMode.Disabled;
            _ent_mat.TransmissionEnabled = true;
            _ent_mat.Transmission = new Color(0.5f, 0.5f, 0.5f, 1);
            
            ent_mat = _ent_mat;
        }
        if(water_mat == null)
        {
            var _water_mat = new SpatialMaterial();
            
            _water_mat.FlagsUnshaded = true;
            _water_mat.FlagsTransparent = false;
            _water_mat.AlbedoColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            _water_mat.ParamsBlendMode = SpatialMaterial.BlendMode.Add;
            _water_mat.ParamsDepthDrawMode = SpatialMaterial.DepthDrawMode.Always;
            _water_mat.ParamsGrow = true;
            _water_mat.ParamsGrowAmount = -0.001f;
            
            water_mat = _water_mat;
            
            var _water_mat2 = new SpatialMaterial();
            
            _water_mat2.AlbedoColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
            _water_mat2.FlagsTransparent = true;
            // built-in godot screenspace reflection doesn't work with non-opaque-mix materials
            // so no point leaving specularity enabled
            _water_mat2.ParamsSpecularMode = SpatialMaterial.SpecularMode.Disabled;
            _water_mat2.ParamsDiffuseMode = SpatialMaterial.DiffuseMode.Lambert;
            _water_mat2.ParamsBlendMode = SpatialMaterial.BlendMode.Mix;
            _water_mat2.ParamsDepthDrawMode = SpatialMaterial.DepthDrawMode.Always;
            _water_mat2.ParamsCullMode = SpatialMaterial.CullMode.Disabled;
            _water_mat2.ParamsGrow = true;
            _water_mat2.ParamsGrowAmount = -0.001f;
            
            water_mat2 = _water_mat2;
            
            water_mat.NextPass = water_mat2;
            water_mat.RenderPriority = 1;
            water_mat2.RenderPriority = 2;
        }
        
        noise = new OpenSimplexNoise();
        noise.Seed = world_seed;
        noise.Octaves = 7;
        noise.Period = 64;
        noise.Persistence = 1.0f;
        noise.Lacunarity = 1.5f;
        
        Disable(this);
        Disable(meshinstance);
        Disable(collider);
        Disable(ent_meshinstance);
        Disable(ent_collider);
        Disable(water_meshinstance);
        
        InitInfos();
    }
    static void Disable(Node node)
    {
        node.SetProcess(false);
        node.SetProcessInput(false);
        node.SetProcessInternal(false);
        node.SetProcessUnhandledInput(false);
        node.SetPhysicsProcess(false);
        node.SetPhysicsProcessInternal(false);
    }
    public byte get_base_tile(Vector3 coord)
    {
        if(!bounds.HasPoint(coord))
            return 0;
        var index = (int)(coord.x + coord.z*size + coord.y*size*size);
        return base_data[index];
    }
    public byte get_tile(Vector3 coord)
    {
        if(!bounds.HasPoint(coord))
            return 0;
        var index = (int)(coord.x + coord.z*size + coord.y*size*size);
        return data[index];
    }
    public void set_tile_raw(Vector3 coord, int tile)
    {
        if(!bounds.HasPoint(coord))
            return;
        var index = (int)(coord.x + coord.z*size + coord.y*size*size);
        data[index] = (byte)tile;
        water[index] = 0;
        water_dirty = true;
    }
    
    void dirty_water_around(Vector3 global_coord)
    {
        GDDictionary chunks = (GDDictionary)world.Get("chunks");
        foreach(var dir in new[]{Vector3.Up, Vector3.Down,
                                 Vector3.Left, Vector3.Right,
                                 Vector3.Forward, Vector3.Back})
        {
            var chunk_coord = to_chunk_coord(global_coord + dir);
            if(chunks.Contains(chunk_coord))
            {
                if(dir != Vector3.Up) // above doesn't care what below is doing logic-wize
                    ((VoxelChunkAlt)chunks[chunk_coord]).water_dirty = true;
                ((VoxelChunkAlt)chunks[chunk_coord]).water_mesh_dirty = true;
            }
        }
    }
    public void set_tile(Vector3 coord, int tile)
    {
        set_tile_raw(coord, tile);
        dirty_water_around(position + coord);
    }
    public byte get_water(Vector3 coord)
    {
        if(!bounds.HasPoint(coord))
            return 0;
        var index = (int)(coord.x + coord.z*size + coord.y*size*size);
        return water[index];
    }
    public void set_water_raw(Vector3 coord, int value)
    {
        if(!bounds.HasPoint(coord))
            return;
        var index = (int)(coord.x + coord.z*size + coord.y*size*size);
        if(value != water[index])
            water_mesh_dirty = true;
        water[index] = (byte)value;
        water_dirty = true;
    }
    public void set_water(Vector3 coord, int value)
    {
        set_water_raw(coord, value);
        dirty_water_around(position + coord);
    }
    
    public int get_ent(Vector3 coord)
    {
        if(!bounds.HasPoint(coord))
            return 0;
        if(!ents.ContainsKey(coord))
            return 0;
        return ents[coord];
    }
    public void set_ent_raw(Vector3 coord, int ent)
    {
        if(!bounds.HasPoint(coord))
            return;
        
        if(ents.ContainsKey(coord))
            ents.Remove(coord);
        if(ent > 0)
            ents.Add(coord, ent);
    }
    public void set_ent(Vector3 coord, int ent)
    {
        set_ent_raw(coord, ent);
        
        var index = (int)(coord.x + coord.z*size + coord.y*size*size);
        water[index] = 0;
        dirty_water_around(position + coord);
    }
    
    bool water_dirty = false;
    bool water_mesh_dirty = false;
    
    float noise_above_ground_unscale = 64.0f;
    //float noise_above_ground_unscale = 512.0f;
    //float noise_above_ground_unscale = 128.0f;
    public void configure_noise(float y, OpenSimplexNoise noise)
    {
        //y += 0.1f;
        //y = Mathf.Ceil(y/2.0f)*2.0f;
        y += 16.0f;
        if(y > 0.0f)
            noise.Persistence = 1.0f - Mathf.Clamp(y/(noise_above_ground_unscale/2.0f), 0.0f, 1.0f)*0.3f;
        else
            noise.Persistence = 1.0f;
    }
    public float calculate_noise(Vector3 coord, OpenSimplexNoise noise)
    {
        //coord += Vector3.One*0.1f;
        //coord = (coord/2.0f).Ceil()*2.0f;
        float density = noise.GetNoise3dv(coord);
        
        density += 0.05f;
        
        coord.y -= 2.0f;
        
        if(coord.y > 0.0f)
        {
            density *= 1.0f - Mathf.Clamp(coord.y/noise_above_ground_unscale, 0.0f, 0.5f);
            density -= coord.y/noise_above_ground_unscale;
        }
        else
        {
            //density -= Math.Max(coord.y/16.0f, -0.10f);
            density -= coord.y/(noise_above_ground_unscale/2.0f);
        }
        
        return density;
    }
    
    ulong hashify(int world_seed, Vector3 pos)
    {
        return (ulong)GD.Hash(world_seed) ^ (ulong)GD.Hash(pos);
    }
    
    public void generate_land(int world_seed, Vector3 pos)
    {
        var grass_scene = GD.Load<PackedScene>("res://Grass.tscn");
        
        var coord_global = new Vector3();
        for(var y = 0; y < size; y++)
        {
            coord_global.y = y + pos.y;
            configure_noise(coord_global.y, noise);
            for(var z = 0; z < size; z++)
            {
                coord_global.z = z + pos.z;
                for(var x = 0; x < size; x++)
                {
                    coord_global.x = x + pos.x;
                    var density = calculate_noise(coord_global, noise);
                    
                    var coord = new Vector3(x, y, z);
                    if(density < 0.0)
                    {
                        set_tile_raw(coord, 0);
                        if(coord_global.y < 0.0f)
                            set_water_raw(coord, 16);
                    }
                    else
                        set_tile_raw(coord, 65);
                }
            }
        }
        base_data = (byte[])data.Clone();
    }
    
    ArrayList get_tree_data(int world_seed)
    {
        var pos = position;
        var ret = new ArrayList();
        if(pos.y < 0)
            return ret;
        
        var noise = new OpenSimplexNoise();
        noise.Seed = world_seed + 1;
        noise.Octaves = 5;
        noise.Period = 32;
        noise.Persistence = 0.9f;
        noise.Lacunarity = 2.0f;
        
        var rng = new RandomNumberGenerator();
        rng.Seed = hashify(world_seed, pos);
        
        var tree_count_f = (noise.GetNoise2d(pos.x/size, pos.z/size)*3.0f + rng.Randf()*6.0f);
        tree_count_f *= size*size;
        tree_count_f /= 16*16;
        var tree_count = (int)tree_count_f;
        
        var lower_chunk  = (VoxelChunkAlt)world.Call("get_chunk", position - new Vector3(0, size, 0));
        
        for(var i = 0; i < tree_count; i++)
        {
            var x = Mathf.Floor(rng.Randf()*16.0f);
            var z = Mathf.Floor(rng.Randf()*16.0f);
            var found = false;
            foreach(Vector3 coord in ret)
            {
                if(Math.Abs(x-coord.x) <= 1 && Math.Abs(z-coord.z) <= 1)
                    found = true;
            }
            if(!found)
            {
                for(var y = size-1; y >= 0; y--)
                {
                    var above = get_base_tile(new Vector3(x, y, z));
                    var below = get_base_tile(new Vector3(x, y-1, z));
                    if(y == 0)
                    {
                        below = lower_chunk.get_base_tile(new Vector3(x, size-1, z));
                    }
                    if(above == 0 && below != 0)
                    {
                        ret.Add(new Vector3(x, y, z));
                        break;
                    }
                }
            }
        }
        
        return ret;
    }
    
    public void generate_objects(int world_seed)
    {
        var pos = position;
        objects_generated = true;
        
        var higher_chunk = (VoxelChunkAlt)world.Call("get_chunk", position + new Vector3(0,  size, 0));
        var lower_chunk  = (VoxelChunkAlt)world.Call("get_chunk", position + new Vector3(0, -size, 0));
        for(var z = 0; z < size; z++)
        {
            for(var x = 0; x < size; x++)
            {
                for(var y = size; y >= 0; y--)
                {
                    var coord = new Vector3(x, y, z);
                    var global_coord = coord+pos;
                    var above = get_base_tile(coord);
                    if(y == size)
                    {
                        above = higher_chunk.get_base_tile(new Vector3(x, 0, z));
                    }
                    var below = get_base_tile(new Vector3(x, y-1, z));
                    if(y == 0)
                    {
                        below = lower_chunk.get_base_tile(new Vector3(x, size-1, z));
                    }
                    if(above == 0 && below == 65)
                    {
                        if(y > 0 && global_coord.y >= 0)
                        {
                            set_tile_raw(new Vector3(x, y-1, z), 66);
                        }
                        if(y < size && y+pos.y >= 0.0 && noise.GetNoise2d(global_coord.x*28.165f, global_coord.z*28.165f) > 0.2)
                        {
                            if(noise.GetNoise3dv(global_coord*5.0f) > -0.1f)
                                set_ent_raw(coord, 1);
                            else
                                set_ent_raw(coord, 2);
                        }
                    }
                }
            }
        }
        
        var chunk_coords = new ArrayList();
        var range = 1;
        for(int c_y = -range; c_y <= range; c_y++)
        {
            for(int c_z = -range; c_z <= range; c_z++)
            {
                for(int c_x = -range; c_x <= range; c_x++)
                {
                    chunk_coords.Add(pos + new Vector3(c_x, c_y, c_z)*size);
                }
            }
        }
        foreach(Vector3 chunk_coord in chunk_coords)
        {
            var chunk = (VoxelChunkAlt)world.Call("get_chunk", chunk_coord);
            var trees = chunk.get_tree_data(world_seed);
            foreach(Vector3 _coord in trees)
            {
                Vector3 coord = _coord;
                var rng = new RandomNumberGenerator();
                rng.Seed = (ulong)GD.Hash(chunk_coord + coord);
                coord += chunk_coord - pos;
                
                var extra = rng.RandiRange(0, 1);
                var bias = rng.RandiRange(1, 2);
                var bias2 = rng.RandiRange(0, 1);
                bias -= bias2;
                extra += bias2;
                extra = Math.Min(extra, 1);
                
                var max = rng.RandiRange(6, 9);
                var leaves_start = rng.RandiRange(2, 3);
                var x = 0;
                var z = 0;
                for(var y = 0; y <= max+extra; y += 1)
                {
                    var newcoord = coord + new Vector3(x, y, z);
                    if(y < max && get_tile(newcoord) == 0)
                        set_tile_raw(newcoord, 10);
                    
                    if(y >= leaves_start)
                    {
                        var leaves_size = (max-y+extra+bias+1)/3;
                        if(rng.Randi()%3 == 0)
                        {
                            if(leaves_size > 1 && y < max)
                                leaves_size -= 1;
                            else if(leaves_size > 0 && y >= max)
                                leaves_size -= 1;
                        }
                        for(var _z = -leaves_size; _z <= leaves_size; _z++)
                        {
                            for(var _x = -leaves_size; _x <= leaves_size; _x++)
                            {
                                // skip corners 75% of the time
                                if(leaves_size > 0 && Math.Abs(_x) == leaves_size && Math.Abs(_z) == leaves_size && rng.Randi()%4 > 0)
                                    continue;
                                newcoord = coord + new Vector3(x+_x, y, z+_z);
                                if(get_tile(newcoord) == 0)
                                    set_tile_raw(newcoord, 20);
                            }
                        }
                    }
                    
                    if(y%2 == 1 && y+2 < max)
                    {
                        if(rng.Randi() % 8 == 0)
                            x += ((int)(rng.Randi() % 2))*2-1;
                        if(rng.Randi() % 8 == 0)
                            z += ((int)(rng.Randi() % 2))*2-1;
                    }
                }
            }
        }
    }
    public void remesh(int divide_res)
    {
        remesh_world(divide_res);
        remesh_ents();
        if(water_mesh_dirty)
            remesh_water();
    }
    void for_dirs(Action<Vector3, Basis, Vector3, Vector3, Vector3, Vector3, Vector3> f)
    {
        foreach(Vector3 dir in new []{
                    Vector3.Up, Vector3.Down,
                    Vector3.Left, Vector3.Right,
                    Vector3.Forward, Vector3.Back
                })
        {
            Basis basis;
            if(dir == Vector3.Up)
                basis = new Basis(new Vector3( 1, 0, 0), new Vector3(0, 0, 1), new Vector3( 0,-1, 0));
            else if(dir == Vector3.Down)
                basis = new Basis(new Vector3( 1, 0, 0), new Vector3(0, 0,-1), new Vector3( 0, 1, 0));
            else if(dir == Vector3.Left)
                basis = new Basis(new Vector3( 0, 0,-1), new Vector3(0, 1, 0), new Vector3( 1, 0, 0));
            else if(dir == Vector3.Right)
                basis = new Basis(new Vector3( 0, 0, 1), new Vector3(0, 1, 0), new Vector3(-1, 0, 0));
            else if(dir == Vector3.Forward)
                basis = new Basis(new Vector3( 1, 0, 0), new Vector3(0, 1, 0), new Vector3( 0, 0, 1));
            else
                basis = new Basis(new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3( 0, 0,-1));
            
            var c1 = basis.Xform(new Vector3(-0.5f, +0.5f, -0.5f));
            var c2 = basis.Xform(new Vector3(-0.5f, -0.5f, -0.5f));
            var c3 = basis.Xform(new Vector3(+0.5f, +0.5f, -0.5f));
            var c4 = basis.Xform(new Vector3(+0.5f, -0.5f, -0.5f));
            
            var normal = basis.Xform(new Vector3(0.0f, 0.0f, -1.0f));
            
            f(dir, basis, c1, c2, c3, c4, normal);
        }
    }
    int current_res = 1;
    public void remesh_world(int divide_res)
    {
        divide_res = Math.Max(1, divide_res);
        current_res = divide_res;
        
        water_meshinstance.Visible = divide_res == 1;
        ent_meshinstance.Visible = divide_res == 1;
        
        var time0 = OS.GetTicksUsec();

        var verts = new ArrayList();
        var normals = new ArrayList();
        var uvs = new ArrayList();
        var uv2s = new ArrayList();
        var indexes = new ArrayList();
        
        var max_cell_count_estimate = size*size*size/2;
        verts.Capacity   = max_cell_count_estimate*6*4;
        normals.Capacity = max_cell_count_estimate*6*4;
        uvs.Capacity     = max_cell_count_estimate*6*4;
        uv2s.Capacity    = max_cell_count_estimate*6*4;
        indexes.Capacity = max_cell_count_estimate*6*6;
        
        float uva = 0.0f;
        float uvb = 1.0f;
        
        var time1 = OS.GetTicksUsec();
        
        var index = 0;
        for_dirs((dir, basis, _c1, _c2, _c3, _c4, normal) =>
        {
            int side;
            if(dir == Vector3.Forward || dir == Vector3.Back)
                side = 0;
            else if(dir == Vector3.Left || dir == Vector3.Right)
                side = 1;
            else
                side = 2;
            
            var _uv1 = new Vector2(uvb, uva);
            var _uv2 = new Vector2(uvb, uvb);
            var _uv3 = new Vector2(uva, uva);
            var _uv4 = new Vector2(uva, uvb);
            
            var rightwards = basis.Xform(new Vector3(1, 0, 0));
            var leftwards  = new Vector3();
            var forwards   = basis.Xform(new Vector3(0, 0, -1));
            
            basis.x = basis.x.Abs();
            basis.y = basis.y.Abs();
            basis.z = basis.z.Abs();
            
            var x_offs = basis.Xform(new Vector3(1, 0, 0));
            var y_offs = basis.Xform(new Vector3(0, 1, 0));
            var z_offs = basis.Xform(new Vector3(0, 0, 1));
            
            if(x_offs != rightwards)
            {
                leftwards = -rightwards;
                rightwards = new Vector3();
            }
            
            var vh = new Vector3(0.5f, 0.5f, 0.5f);
            _c1 = (_c1 + vh) * divide_res - vh;
            _c2 = (_c2 + vh) * divide_res - vh;
            _c3 = (_c3 + vh) * divide_res - vh;
            _c4 = (_c4 + vh) * divide_res - vh;
            
            _uv1 *= divide_res;
            _uv2 *= divide_res;
            _uv3 *= divide_res;
            _uv4 *= divide_res;
            
            var svh = new Vector3(size, size, size)/2.0f;
            
            var coord = new Vector3();
            for(var y = 0; y < size+divide_res; y += divide_res)
            {
                if(side == 2)
                    coord.z = y;
                else
                    coord.y = y;
                for(var z = 0; z < size+divide_res; z += divide_res)
                {
                    if(side == 0)
                        coord.z = z;
                    else if(side == 1)
                        coord.x = z;
                    else
                        coord.y = z;
                    for(var x = 0; x < size+divide_res; x += divide_res)
                    {
                        if(side == 1)
                            coord.z = x;
                        else
                            coord.x = x;
                        
                        var tile = get_tile(coord);
                        if(tile == 0)
                            continue;
                        
                        Vector3 deeper = coord + forwards*divide_res;
                        if(!infos_transparent[get_tile(deeper)])
                            continue;
                        
                        var start = x;
                        var start_tile = tile;
                        
                        while(tile == start_tile && x < size+divide_res)
                        {
                            x += divide_res;
                            coord += x_offs * divide_res;
                            tile = get_tile(coord);
                            deeper += x_offs * divide_res;
                            if(!infos_transparent[get_tile(deeper)])
                                break;
                        }
                        x -= divide_res;
                        coord -= x_offs * divide_res;
                        tile = start_tile;
                        
                        var c1 = _c1 + coord + (start - x)*rightwards;
                        var c2 = _c2 + coord + (start - x)*rightwards;
                        var c3 = _c3 + coord + (start - x)*leftwards;
                        var c4 = _c4 + coord + (start - x)*leftwards;
                        
                        Vector2 offset;
                        if(dir == Vector3.Up)
                        {
                            offset = infos_top[tile];
                            if(divide_res != 1)
                            {
                                var tile2 = get_tile(coord + Vector3.Up*(divide_res-1));
                                if(tile2 != 0)
                                    offset = infos_top[tile2];
                            }
                        }
                        else if (dir == Vector3.Down)
                            offset = infos_bottom[tile];
                        else
                            offset = infos_side[tile];
                        
                        var uv2_uv = offset;
                        
                        var uv1 = _uv1 + new Vector2(x - start, 0);
                        var uv2 = _uv2 + new Vector2(x - start, 0);
                        var uv3 = _uv3;
                        var uv4 = _uv4;
                        
                        verts.Add(c1);
                        verts.Add(c2);
                        verts.Add(c3);
                        verts.Add(c4);
                        
                        uvs.Add(uv1);
                        uvs.Add(uv2);
                        uvs.Add(uv3);
                        uvs.Add(uv4);
                        
                        uv2s.Add(uv2_uv);
                        uv2s.Add(uv2_uv);
                        uv2s.Add(uv2_uv);
                        uv2s.Add(uv2_uv);
                        
                        normals.Add(normal);
                        normals.Add(normal);
                        normals.Add(normal);
                        normals.Add(normal);
                        
                        indexes.Add(index);
                        indexes.Add(index+1);
                        indexes.Add(index+2);
                        
                        indexes.Add(index+2);
                        indexes.Add(index+1);
                        indexes.Add(index+3);
                        
                        index += 4;
                    }
                }
            }
        });
        
        var time2 = OS.GetTicksUsec();
        
        meshinstance.Mesh = new ArrayMesh();
        collider.ShapeOwnerClearShapes(0);
        
        if(verts.Count > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)ArrayMesh.ArrayType.Max);
            //GD.Print(arrays);
            arrays[(int)ArrayMesh.ArrayType.Vertex] = verts.ToArray(typeof(Vector3));
            arrays[(int)ArrayMesh.ArrayType.TexUv]  = uvs.ToArray(typeof(Vector2));
            arrays[(int)ArrayMesh.ArrayType.TexUv2] = uv2s.ToArray(typeof(Vector2));
            arrays[(int)ArrayMesh.ArrayType.Normal] = normals.ToArray(typeof(Vector3));
            arrays[(int)ArrayMesh.ArrayType.Index]  = indexes.ToArray(typeof(int));
            var asdf = ((ArrayMesh)meshinstance.Mesh);
            asdf.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);//, new Godot.Collections.Array(), 0);
            
            if(current_res == 1)
            {
                var collision_verts = new ArrayList();
                var shape = new ConcavePolygonShape();
                
                // optimized collision scanline/rect generation
                foreach(var dir in new []{
                            Vector3.Up,
                            Vector3.Left,
                            Vector3.Right,
                        })
                {
                    Basis basis;
                    if(dir == Vector3.Up)
                        basis = new Basis(new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0));
                    else if(dir == Vector3.Left)
                        basis = new Basis(new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(1, 0, 0));
                    else
                        basis = new Basis(new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1));
                    
                    for(var z = 0; z <= size; z++)
                    {
                        var starts = new ArrayList();
                        var ends = new ArrayList();
                        for(var y = 0; y <= size; y++)
                        {
                            var start = -1;
                            for(var x = 0; x <= size; x++)
                            {
                                Vector3 coord = basis.Xform(new Vector3(x, y, z));
                                Vector3 deeper = basis.Xform(new Vector3(0, 0, 1));
                                var tile = get_tile(coord);
                                var tile2 = get_tile(coord - deeper);
                                if((tile == 0) != (tile2 == 0)) // FIXME is_solid
                                {
                                    if(start < 0)
                                        start = x;
                                }
                                else if(start >= 0)
                                {
                                    starts.Add(new Vector3(start, y, z));
                                    ends.Add(new Vector3(x, y+1, z));
                                    start = -1;
                                }
                            }
                            if(start >= 0)
                            {
                                starts.Add(new Vector3(start, y, z));
                                ends.Add(new Vector3(size, y+1, z));
                            }
                        }
                        
                        var i = -1;
                        while(i+1 < starts.Count)
                        {
                            i += 1;
                            var curr_a = (Vector3)starts[i];
                            var curr_b = (Vector3)ends[i];
                            var j = i;
                            while(j+1 < starts.Count)
                            {
                                j += 1;
                                var next_a = (Vector3)starts[j];
                                var next_b = (Vector3)ends[j];
                                if(curr_a.x == next_a.x && curr_b.x == next_b.x && curr_b.y == next_a.y)
                                {
                                    curr_b.y = next_b.y;
                                    ends[i] = curr_b;
                                    starts.RemoveAt(j);
                                    ends.RemoveAt(j);
                                    j -= 1;
                                }
                            }
                        }
                        
                        var hv = new Vector3(0.5f, 0.5f, 0.5f);
                        for(var j = 0; j < starts.Count; j++)
                        {
                            var curr_a = (Vector3)starts[j] - hv;
                            var curr_b = (Vector3)ends[j] - hv;
                            var curr_q = curr_a;
                            curr_q.y = curr_b.y;
                            var curr_r = curr_a;
                            curr_r.x = curr_b.x;
                            
                            curr_a = basis.Xform(curr_a);
                            curr_b = basis.Xform(curr_b);
                            curr_q = basis.Xform(curr_q);
                            curr_r = basis.Xform(curr_r);
                            
                            collision_verts.Add(curr_a);
                            collision_verts.Add(curr_r);
                            collision_verts.Add(curr_q);
                            
                            collision_verts.Add(curr_q);
                            collision_verts.Add(curr_r);
                            collision_verts.Add(curr_b);
                        }
                    }
                }
                if(collision_verts.Count > 0)
                {
                    shape.Data = (Vector3[])collision_verts.ToArray(typeof(Vector3));
                    collider.ShapeOwnerAddShape(0, shape);
                }
            }
        }
        
        meshinstance.MaterialOverride = mat;
        ForceUpdateTransform();
        collider.ForceUpdateTransform();
    }
    
    Vector3 to_chunk_coord(Vector3 global_coord)
    {
        global_coord += new Vector3(0.5f, 0.5f, 0.5f);
        var chunk_coord = (global_coord/size).Floor()*size;
        // flush negative zeroes
        if(chunk_coord.x == 0.0f)
            chunk_coord.x = 0.0f;
        if(chunk_coord.y == 0.0f)
            chunk_coord.y = 0.0f;
        if(chunk_coord.z == 0.0f)
            chunk_coord.z = 0.0f;
        return chunk_coord;
    }
    
    /*
    Vector3 global_get_ent_size(GDDictionary chunks, Vector3 global_coord)
    {
        global_coord = global_coord.Round();
        Vector3 chunk_coord = to_chunk_coord(global_coord);
        if(chunks.Contains(chunk_coord))
            return ((VoxelChunkAlt)chunks[chunk_coord]).get_ent_size(global_coord-chunk_coord);
        return new Vector3();
    }

    byte global_get_tile(GDDictionary chunks, Vector3 global_coord)
    {
        global_coord = global_coord.Round();
        Vector3 chunk_coord = to_chunk_coord(global_coord);
        if(chunks.Contains(chunk_coord))
            return ((VoxelChunkAlt)chunks[chunk_coord]).get_tile(global_coord-chunk_coord);
        return 0;
    }

    int global_get_ent(GDDictionary chunks, Vector3 global_coord)
    {
        global_coord = global_coord.Round();
        Vector3 chunk_coord = to_chunk_coord(global_coord);
        if(chunks.Contains(chunk_coord))
            return ((VoxelChunkAlt)chunks[chunk_coord]).get_ent(global_coord-chunk_coord);
        return 0;
    }

    byte global_get_water(GDDictionary chunks, Vector3 global_coord)
    {
        global_coord = global_coord.Round();
        Vector3 chunk_coord = to_chunk_coord(global_coord);
        if(chunks.Contains(chunk_coord))
            return ((VoxelChunkAlt)chunks[chunk_coord]).get_water(global_coord-chunk_coord);
        return 0;
    }
    */
    
    bool ContainsGlobalTileCoord(Vector3 coord)
    {
        return bounds.HasPoint(coord - position);
    }
    
    // are you fucking serious
    static object GDGet(GDDictionary dict, object key)
    {
        if(dict.Contains(key))
            return dict[key];
        return null;
    }
    Dictionary<Vector3, byte> water_updates = new Dictionary<Vector3, byte>();
    public void think_step1(float delta)
    {
        if(!water_dirty)
            return;
        
        water_dirty = false;
        GDDictionary chunks = (GDDictionary)world.Get("chunks");
        Dictionary<Vector3, VoxelChunkAlt> dir_chunks = new Dictionary<Vector3, VoxelChunkAlt>();
        
        Action assign_chunks = () => {
            foreach(var dir in new []{
                    new Vector3( 0, 1, 0),
                    new Vector3( 0,-1, 0),
                    new Vector3( 1, 0, 0),
                    new Vector3(-1, 0, 0),
                    new Vector3( 0, 0, 1),
                    new Vector3( 0, 0,-1),
                    new Vector3( 1,-1, 0),
                    new Vector3(-1,-1, 0),
                    new Vector3( 0,-1, 1),
                    new Vector3( 0,-1,-1),
                })
            {
                // any loaded chunk (thus, having this function called on it) should have its neighbors generated already
                // so we don't need to use get_chunk
                dir_chunks.Add(dir, (VoxelChunkAlt)chunks[position + dir*size]);
            }
        };
        
        for(var y = 0; y < size; y++)
        {
            for(var z = 0; z < size; z++)
            {
                for(var x = 0; x < size; x++)
                {
                    var coord = new Vector3(x, y, z);
                    if(get_tile(coord) != 0)
                        continue;
                    
                    var count_neighbors_source = 0;
                    var highest_neighbor = 0;
                    var water_above = false;
                    var old = get_water(coord);
                    if(old > 8) // source block
                        continue;
                    
                    var current = (old <= 8 && old > 0) ? (old-1) : (old);
                    
                    if(dir_chunks.Count == 0)
                        assign_chunks();
                    
                    foreach(Vector3 dir in new []{
                                Vector3.Left, Vector3.Right,
                                Vector3.Forward, Vector3.Back,
                                Vector3.Up,
                            })
                    {
                        var next_coord = coord + dir;
                        byte next_water;
                        if(bounds.HasPoint(next_coord))
                        {
                            next_water = get_water(coord + dir);
                            if(next_water == 0)
                                continue;
                        }
                        else if(dir_chunks.ContainsKey(dir) && dir_chunks[dir].ContainsGlobalTileCoord(position + next_coord))
                        {
                            next_water = dir_chunks[dir].get_water(next_coord - dir*size);
                            if(next_water == 0)
                                continue;
                        }
                        else // neighbor not loaded; treat as 0
                            continue;
                        
                        
                        if(dir.y == 0)
                        {
                            if(next_water == 16)
                            {
                                count_neighbors_source += 1;
                                highest_neighbor = 16;
                            }
                            else
                            {
                                byte under_tile = 0;
                                var next_coord_under = coord + dir + Vector3.Down;
                                if(bounds.HasPoint(next_coord))
                                    under_tile = get_tile(next_coord_under);
                                else if(dir_chunks.ContainsKey(dir) && dir_chunks[dir].ContainsGlobalTileCoord(position + next_coord_under))
                                    under_tile = dir_chunks[dir].get_tile(next_coord_under - dir*size);
                                else if(dir_chunks.ContainsKey(dir + Vector3.Down) && dir_chunks[dir + Vector3.Down].ContainsGlobalTileCoord(position + next_coord_under))
                                    under_tile = dir_chunks[dir + Vector3.Down].get_tile(next_coord_under - (dir + Vector3.Down)*size);
                                // else unloaded, use default 0
                                
                                var on_ground = under_tile != 0;
                                if(on_ground)
                                    highest_neighbor = Math.Max(highest_neighbor, next_water);
                            }
                        }
                        if(dir.y == 1 && next_water > 0)
                            water_above = true;
                    }
                    highest_neighbor = Math.Min(highest_neighbor, 8);
                    if(count_neighbors_source >= 2)
                        current = 16;
                    else if(water_above)
                        current = 8;
                    else
                        current = Math.Max(Math.Max(0, current), highest_neighbor-1);
                    
                    if(current != old)
                        water_updates.Add(coord, (byte)current);
                }
            }
        }
    }
    public void think_step2(float delta)
    {
        var aabb = new AABB();
        foreach(var entry in water_updates)
        {
            var coord = entry.Key;
            var current = entry.Value;
            aabb = aabb.Expand(coord);
            set_water_raw(coord, current);
        }
        if(water_updates.Count > 0)
        {
            dirty_water_around(position + aabb.Position);
            dirty_water_around(position + aabb.End);
        }
        water_updates.Clear();
        if(water_mesh_dirty)
            remesh_water();
    }
    public void think_always(float delta)
    {
        if(water_mesh_dirty)
            remesh_water();
    }
    public void remesh_water()
    {
        water_mesh_dirty = false;
        var water_verts = new ArrayList();
        var water_normals = new ArrayList();
        
        var vscale = 14.0f/16.0f;
        
        GDDictionary chunks = (GDDictionary)world.Get("chunks");
        
        for_dirs((dir, basis, _c1, _c2, _c3, _c4, normal) =>
        {
            // FIXME: use strip meshing for the up/down directions
            VoxelChunkAlt next_chunk    = (VoxelChunkAlt)GDGet(chunks, position + dir*size);
            VoxelChunkAlt up_next_chunk = (VoxelChunkAlt)GDGet(chunks, position + dir*size + Vector3.Up*size);
            VoxelChunkAlt up_chunk      = (VoxelChunkAlt)GDGet(chunks, position + Vector3.Up*size);
            for(var y = 0; y <= size; y++)
            {
                for(var z = 0; z <= size; z++)
                {
                    for(var x = 0; x <= size; x++)
                    {
                        var coord = new Vector3(x, y, z);
                        var current = Math.Min((byte)8, get_water(coord));
                        if(current == 0)
                            continue;
                        var next_coord = coord+dir;
                        byte next;
                        if(next_chunk != null && next_chunk.ContainsGlobalTileCoord(position + next_coord))
                            next = next_chunk.get_tile(next_coord - dir*size);
                        else
                            next = get_tile(next_coord);
                        
                        if(!infos_transparent[next] && dir.y != 1)
                            continue;
                        
                        byte next_current;
                        if(next_chunk != null && next_chunk.ContainsGlobalTileCoord(position + next_coord))
                            next_current = Math.Min((byte)8, next_chunk.get_water(next_coord - dir*size));
                        else
                            next_current = Math.Min((byte)8, get_water(next_coord));
                        
                        current = Math.Min((byte)8, current);
                        next_current = Math.Min((byte)8, next_current);
                        
                        if(dir.y == 1 && next_current > 0)
                            continue;
                        if(dir.y == -1 && next_current >= 8)
                            continue;
                        
                        byte above_current;
                        if(up_chunk != null && up_chunk.ContainsGlobalTileCoord(position + coord + Vector3.Up))
                            above_current = up_chunk.get_water(coord + Vector3.Up);
                        else
                            above_current = get_water(coord + Vector3.Up);
                        
                        byte next_above_current;
                        if(dir.y != 0)
                            next_above_current = 0;
                        else if(up_chunk != null && up_chunk.ContainsGlobalTileCoord(position + next_coord + Vector3.Up))
                            next_above_current = Math.Min((byte)8,      up_chunk.get_water(next_coord + Vector3.Up - Vector3.Up*size));
                        else if(next_chunk != null && next_chunk.ContainsGlobalTileCoord(position + next_coord + Vector3.Up))
                            next_above_current = Math.Min((byte)8,    next_chunk.get_water(next_coord + Vector3.Up - dir*size));
                        else if(up_next_chunk != null && next_chunk.ContainsGlobalTileCoord(position + next_coord + Vector3.Up))
                            next_above_current = Math.Min((byte)8, up_next_chunk.get_water(next_coord + Vector3.Up - dir*size - Vector3.Up*size));
                        else
                            next_above_current = Math.Min((byte)8, get_water(next_coord + Vector3.Up));
                        
                        if(dir.y == 0 && next_current >= current && next_above_current == 0)
                            continue;
                        
                        var strip_start = x;
                        if(dir.y != 0)
                        {
                            var start_current = current;
                            var start_above_current = above_current;
                            
                            while(current == start_current
                                  && (above_current == 0) == (start_above_current == 0)
                                  && x <= size)
                            {
                                x += 1;
                                var new_coord = new Vector3(x, y, z);
                                current = Math.Min((byte)8, get_water(new_coord));
                                above_current = Math.Min((byte)8, get_water(new_coord + Vector3.Up));
                            }
                            x -= 1;
                            current = start_current;
                        }
                        
                        var top = current/8.0f;
                        var bottom = 0.0f;
                        if(dir.y == 0)
                            bottom = next_current/8.0f;
                        
                        if(above_current == 0 && dir.y != -1)
                            top *= vscale;
                        if(next_above_current == 0 && dir.y == 0)
                            bottom *= vscale;
                        
                        var midpoint = (top+bottom)/2.0f;
                        
                        var scale = new Vector3(1 + x - strip_start, top-bottom, 1);
                        var offset = new Vector3((x - strip_start)/2.0f, midpoint - 0.5f, 0);
                        
                        var c1 = _c1 * scale + offset;
                        var c2 = _c2 * scale + offset;
                        var c3 = _c3 * scale + offset;
                        var c4 = _c4 * scale + offset;
                        
                        water_verts.Add(c1 + coord);
                        water_verts.Add(c2 + coord);
                        water_verts.Add(c3 + coord);
                        
                        water_verts.Add(c3 + coord);
                        water_verts.Add(c2 + coord);
                        water_verts.Add(c4 + coord);
                        
                        foreach(var i in new []{0,0,0,0,0,0})
                            water_normals.Add(normal);
                    }
                }
            }
        });
        
        water_meshinstance.Mesh = new ArrayMesh();
        
        if(water_verts.Count > 0)
        {
            GD.Print("made a water mesh");
            GD.Print(water_verts.Count);
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)ArrayMesh.ArrayType.Max);
            //GD.Print(arrays);
            arrays[(int)ArrayMesh.ArrayType.Vertex] = water_verts.ToArray(typeof(Vector3));
            arrays[(int)ArrayMesh.ArrayType.Normal] = water_normals.ToArray(typeof(Vector3));
            var asdf = ((ArrayMesh)water_meshinstance.Mesh);
            asdf.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        ForceUpdateTransform();
        water_meshinstance.MaterialOverride = water_mat;
    }
    public void remesh_ents()
    {
        var ent_verts = new ArrayList();
        var ent_normals = new ArrayList();
        var ent_uvs = new ArrayList();
        var ent_indexes = new ArrayList();
        var ent_collision_verts = new ArrayList();
        var ent_collision_indexes = new ArrayList();
        
        float uva = 0.0f;
        float uvb = 1.0f;
        
        var uv_res = new Vector2(1/16.0f, 1/8.0f);
        
        foreach(var ent_coord in ents.Keys)
        {
            var entnum = ents[ent_coord];
            var ent = entinfos[entnum];
            if(!ent.is_block)
            {
                var i = ent_verts.Count;
                
                ent_verts.Add(new Vector3(-0.5f, 1, -0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                ent_verts.Add(new Vector3(-0.5f, 0, -0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                ent_verts.Add(new Vector3( 0.5f, 1,  0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                ent_verts.Add(new Vector3( 0.5f, 0,  0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                
                ent_verts.Add(new Vector3(-0.5f, 1,  0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                ent_verts.Add(new Vector3(-0.5f, 0,  0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                ent_verts.Add(new Vector3( 0.5f, 1, -0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                ent_verts.Add(new Vector3( 0.5f, 0, -0.5f) * ent.size + ent_coord + new Vector3(0, -0.5f, 0));
                
                ent_normals.Add(new Vector3( 1, 0, -1).Normalized());
                ent_normals.Add(new Vector3( 1, 0, -1).Normalized());
                ent_normals.Add(new Vector3( 1, 0, -1).Normalized());
                ent_normals.Add(new Vector3( 1, 0, -1).Normalized());
                
                ent_normals.Add(new Vector3(-1, 0, -1).Normalized());
                ent_normals.Add(new Vector3(-1, 0, -1).Normalized());
                ent_normals.Add(new Vector3(-1, 0, -1).Normalized());
                ent_normals.Add(new Vector3(-1, 0, -1).Normalized());
                
                ent_uvs.Add(new Vector2(uva, uva) * uv_res + ent.sprite_coord * uv_res);
                ent_uvs.Add(new Vector2(uva, uvb) * uv_res + ent.sprite_coord * uv_res);
                ent_uvs.Add(new Vector2(uvb, uva) * uv_res + ent.sprite_coord * uv_res);
                ent_uvs.Add(new Vector2(uvb, uvb) * uv_res + ent.sprite_coord * uv_res);
                
                ent_uvs.Add(new Vector2(uva, uva) * uv_res + ent.sprite_coord * uv_res);
                ent_uvs.Add(new Vector2(uva, uvb) * uv_res + ent.sprite_coord * uv_res);
                ent_uvs.Add(new Vector2(uvb, uva) * uv_res + ent.sprite_coord * uv_res);
                ent_uvs.Add(new Vector2(uvb, uvb) * uv_res + ent.sprite_coord * uv_res);
                
                ent_indexes.Add(i  );
                ent_indexes.Add(i+1);
                ent_indexes.Add(i+2);
                
                ent_indexes.Add(i+2);
                ent_indexes.Add(i+1);
                ent_indexes.Add(i+3);
                
                i += 4;
                
                ent_indexes.Add(i  );
                ent_indexes.Add(i+1);
                ent_indexes.Add(i+2);
                
                ent_indexes.Add(i+2);
                ent_indexes.Add(i+1);
                ent_indexes.Add(i+3);
                
                add_vert_only_box(ent_collision_verts, ent.collision_size, ent_coord);
            }
        }
        
        ent_meshinstance.Mesh = new ArrayMesh();
        ent_collider.ShapeOwnerClearShapes(0);
        
        if(ent_verts.Count > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)ArrayMesh.ArrayType.Max);
            //GD.Print(arrays);
            arrays[(int)ArrayMesh.ArrayType.Vertex] = ent_verts.ToArray(typeof(Vector3));
            arrays[(int)ArrayMesh.ArrayType.TexUv]  = ent_uvs.ToArray(typeof(Vector2));
            arrays[(int)ArrayMesh.ArrayType.Normal] = ent_normals.ToArray(typeof(Vector3));
            arrays[(int)ArrayMesh.ArrayType.Index]  = ent_indexes.ToArray(typeof(int));
            var asdf = ((ArrayMesh)ent_meshinstance.Mesh);
            asdf.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            
            var shape = new ConcavePolygonShape();
            shape.Data = (Vector3[])ent_collision_verts.ToArray(typeof(Vector3));
            ent_collider.ShapeOwnerAddShape(0, shape);
        }
        ForceUpdateTransform();
        ent_collider.ForceUpdateTransform();
        ent_meshinstance.MaterialOverride = ent_mat;
        
        
        //GD.Print(ent_verts.Count, " ent verts");
    }
    
    public Vector3 get_ent_size(Vector3 ent_coord)
    {
        if(ents.ContainsKey(ent_coord))
        {
            var entnum = ents[ent_coord];
            var ent = entinfos[entnum];
            return ent.collision_size;
        }
        return new Vector3();
    }
    
    public void add_vert_only_box(ArrayList verts, Vector3 size, Vector3 coord)
    {
        verts.Capacity += 36;
        
        var offset = size;
        offset.x = 0;
        offset.z = 0;
        offset.y = -(1.0f - offset.y)/2.0f;
        
        offset += coord;
        
        var i = verts.Count;
        for_dirs((dir, basis, c1, c2, c3, c4, normal) =>
        {
            c1 = c1 * size + offset;
            c2 = c2 * size + offset;
            c3 = c3 * size + offset;
            c4 = c4 * size + offset;
            
            verts.Add(c1);
            verts.Add(c2);
            verts.Add(c3);
            
            verts.Add(c3);
            verts.Add(c2);
            verts.Add(c4);
        });
    }
}
