extends Spatial
class_name GameWorld

# Declare member variables here. Examples:
# var a = 2
# var b = "text"

class TileType:
    var coord_top : Vector2 = Vector2(1, 4)
    var coord_side : Vector2 = Vector2(1, 4)
    var coord_bottom : Vector2 = Vector2(1, 4)
    var is_transparent : bool = false
    func _init(top_x, top_y, side_x, side_y, bottom_x, bottom_y, transparent : bool):
        coord_top = Vector2(top_x, top_y)
        coord_side = Vector2(side_x, side_y)
        coord_bottom = Vector2(bottom_x, bottom_y)
        is_transparent = transparent

var tile_type_info = {
     0 : TileType.new(15, 7,  15, 7,  15, 7, true),
    65 : TileType.new( 1, 4,   1, 4,   1, 4, false),
    66 : TileType.new( 7, 4,   0, 4,   1, 4, false),
    10 : TileType.new(15, 2,   8, 7,  15, 2, true),
    20 : TileType.new( 9, 5,   9, 5,   9, 5, true),
}

var tile_item_mats = {}

const CHUNK_SIZE = 16

onready var dummy_physics_space = PhysicsServer.space_create()
func oopsie_body(body : PhysicsBody):
    PhysicsServer.body_set_space(body.get_rid(), dummy_physics_space)
    PhysicsServer.body_set_space(body.get_rid(), get_world().space)

#const WORLD_SIZE_XZ = 128
const WORLD_SIZE_Y = 16

var chunks = {}
var loaded_chunks = {}

func to_chunk_coord(global_coord : Vector3):
    global_coord = global_coord.round()
    var chunk_coord = (global_coord/CHUNK_SIZE).floor()*CHUNK_SIZE
    # flush negative zeroes
    if chunk_coord.x == 0.0:
        chunk_coord.x = 0.0
    if chunk_coord.y == 0.0:
        chunk_coord.y = 0.0
    if chunk_coord.z == 0.0:
        chunk_coord.z = 0.0
    return chunk_coord

func get_ent_size(global_coord : Vector3):
    global_coord = global_coord.round()
    var chunk_coord = to_chunk_coord(global_coord)
    if chunk_coord in chunks:
        return chunks[chunk_coord].get_ent_size(global_coord-chunk_coord)
    return Vector3()

func get_tile(global_coord : Vector3):
    global_coord = global_coord.round()
    var chunk_coord = to_chunk_coord(global_coord)
    if chunk_coord in chunks:
        return chunks[chunk_coord].get_tile(global_coord-chunk_coord)
    return 0

func get_ent(global_coord : Vector3):
    global_coord = global_coord.round()
    var chunk_coord = to_chunk_coord(global_coord)
    if chunk_coord in chunks:
        return chunks[chunk_coord].get_ent(global_coord-chunk_coord)
    return 0

func get_water(global_coord : Vector3):
    global_coord = global_coord.round()
    var chunk_coord = to_chunk_coord(global_coord)
    if chunk_coord in chunks:
        return chunks[chunk_coord].get_water(global_coord-chunk_coord)
    return 0

func set_tile(global_coord : Vector3, tile : int):
    global_coord = global_coord.round()
    var chunk_coord = to_chunk_coord(global_coord)
    ensure_loaded(chunk_coord)
    var chunk = chunks[chunk_coord]
    chunk.set_tile(global_coord-chunk_coord, tile)
    chunk.remesh_world()
    chunk.remesh_water()

func set_ent(global_coord : Vector3, ent : int):
    global_coord = global_coord.round()
    var chunk_coord = to_chunk_coord(global_coord)
    ensure_loaded(chunk_coord)
    var chunk = chunks[chunk_coord]
    chunk.set_ent(global_coord-chunk_coord, ent)
    chunk.remesh_ents()

func set_water(global_coord : Vector3, level : int):
    global_coord = global_coord.round()
    var chunk_coord = to_chunk_coord(global_coord)
    ensure_loaded(chunk_coord)
    var chunk = chunks[chunk_coord]
    chunk.set_water(global_coord-chunk_coord, level)
    chunk.remesh_water()

func dirty_water_around(global_coord : Vector3):
    for dir in [Vector3.UP, Vector3.DOWN,
                Vector3.LEFT, Vector3.RIGHT,
                Vector3.FORWARD, Vector3.BACK]:
        var chunk_coord = to_chunk_coord(global_coord + dir)
        if chunk_coord in chunks:
            if dir != Vector3.UP: # above doesn't care what below is doing logic-wize
                chunks[chunk_coord].water_dirty = true
            chunks[chunk_coord].water_mesh_dirty = true

var world_seed = 0

var VoxelChunk = load("res://VoxelChunkAlt.cs")

func ensure_land_generated(chunk_coord):
    chunk_coord = to_chunk_coord(chunk_coord)
    if not chunk_coord in chunks:
        var chunk = VoxelChunk.new(world_seed, self, CHUNK_SIZE, chunk_coord)
        chunks[chunk_coord] = chunk
        var timer = Stopwatch.new()
        chunk.generate_land(world_seed, chunk_coord)
        landgen_time += timer.stop()

func ensure_loaded(chunk_coord):
    chunk_coord = to_chunk_coord(chunk_coord)
    if not chunk_coord in loaded_chunks:
        if chunk_coord in chunks:
            var chunk = chunks[chunk_coord]
            loaded_chunks[chunk_coord] = chunk
            add_chunk_child(chunk, chunk_coord)
            chunk.global_translation = chunk_coord
            if !chunk.objects_generated:
                var lg_before = landgen_time
                var timer = Stopwatch.new()
                chunk.generate_objects(world_seed)
                fullgen_time += timer.cycle()
                var lg_after = landgen_time
                fullgen_time -= lg_after-lg_before # discount landgen time
                chunk.remesh()
                remesh_time += timer.stop()
            return
        var chunk = VoxelChunk.new(world_seed, self, CHUNK_SIZE, chunk_coord)
        chunks[chunk_coord] = chunk
        loaded_chunks[chunk_coord] = chunk
        add_chunk_child(chunk, chunk_coord)
        chunk.global_translation = chunk_coord
        var timer = Stopwatch.new()
        chunk.generate_land(world_seed, chunk_coord)
        landgen_time += timer.cycle()
        chunk.generate_objects(world_seed)
        fullgen_time += timer.cycle()
        chunk.remesh()
        remesh_time += timer.stop()

var _removal_nonce = 0
func remove_stale_from_queue(player_chunk_coord):
    _removal_nonce += 1
    var local_nonce = _removal_nonce
    
    var budget = 0.001
    
    var spent_time = 0.0
    var spent_budget = 0.0
    
    var i = 0
    var asdfg = 0
    var frames_taken = 0
    while i < len(near_unloaded_chunks):
        var timer = Stopwatch.new()
        
        var chunk_coord = near_unloaded_chunks[i]
        if bicone_distance(player_chunk_coord, chunk_coord)/CHUNK_SIZE > render_distance:
            near_unloaded_chunks.remove(i)
            near_unloaded_chunks_set.erase(chunk_coord)
            asdfg += 1
            i -= 1
        i += 1
        
        var time = timer.cycle()
        spent_time += time
        spent_budget += time
        if spent_budget > budget:
            spent_budget = 0.0
            frames_taken += 1
            yield(get_tree(), "idle_frame")
            if local_nonce != _removal_nonce:
                break
        
    if asdfg > 0:
        #print("removed from queue: ", asdfg)
        pass
    
    #print("!!! queue removal time... ", spent_time, " frames taken... ", frames_taken)

# warning-ignore:integer_division
var render_distance = 4*16/CHUNK_SIZE
var prev_player_chunk_coord = null
var near_unloaded_chunks = []
var near_unloaded_chunks_set = {}
var prev_render_distance = render_distance
var near_free_chunks = []
var to_unload = []
var player_pos_f = null # track player position with leeway as a form of hysterisis
var player_pos_f_dist = 2 # leeway of 4x4 area around the player
func check_player_nearby_chunks():
# warning-ignore:integer_division
    render_distance = 4*16/CHUNK_SIZE
    var changed = render_distance != prev_render_distance
    prev_render_distance = render_distance
    
    var player : Spatial = get_tree().get_nodes_in_group("Player")[0]
    
    var player_pos = player.global_translation
    if not player_pos_f is Vector3:
        player_pos_f = player_pos
    player_pos_f.x = clamp(player_pos_f.x, player_pos.x-player_pos_f_dist, player_pos.x+player_pos_f_dist)
    player_pos_f.y = clamp(player_pos_f.y, player_pos.y-player_pos_f_dist, player_pos.y+player_pos_f_dist)
    player_pos_f.z = clamp(player_pos_f.z, player_pos.z-player_pos_f_dist, player_pos.z+player_pos_f_dist)
    
    var future_player_chunk_coord = to_chunk_coord(player_pos_f + player.velocity*0.5)
    var player_chunk_coord = to_chunk_coord(player_pos_f)
    
    var changed_pos = player_chunk_coord != prev_player_chunk_coord
    
    var unload_distance = render_distance+2
    
    ensure_loaded(player_chunk_coord + Vector3(0, CHUNK_SIZE, 0))
    ensure_loaded(player_chunk_coord)
    ensure_loaded(future_player_chunk_coord + Vector3(0, CHUNK_SIZE, 0))
    ensure_loaded(future_player_chunk_coord)
    
    var found = false
    
    if (not prev_player_chunk_coord is Vector3) or changed_pos or changed:
        #print("resorting... ", player_chunk_coord, " ", prev_player_chunk_coord)
        prev_player_chunk_coord = player_chunk_coord
        
        
        var temp_render_dist_max = render_distance*CHUNK_SIZE
        var temp_dist_int = int(ceil(render_distance))
        near_free_chunks = []
        var asdf = 0
        for y in range(-temp_dist_int, temp_dist_int+1):
            for z in range(-temp_dist_int, temp_dist_int+1):
                for x in range(-temp_dist_int, temp_dist_int+1):
                    var chunk_coord = player_chunk_coord + Vector3(x, y, z)*CHUNK_SIZE
                    var dist = bicone_distance(player_chunk_coord, chunk_coord)
                    if dist > temp_render_dist_max:
                        continue
                    asdf += 1
                    if not chunk_coord in loaded_chunks:
                        if chunk_coord in chunks and chunks[chunk_coord].objects_generated:
                            near_free_chunks.push_back(chunk_coord)
                        else:
                            if not chunk_coord in near_unloaded_chunks_set:
                                near_unloaded_chunks.push_back(chunk_coord)
                            near_unloaded_chunks_set[chunk_coord] = null
        
        var timer = Stopwatch.new()
        
        var dists = {}
        var dists_list = []
        for chunk in near_unloaded_chunks:
            var dist = -bicone_distance(chunk, player_chunk_coord)
            if not dist in dists:
                dists[dist] = []
                dists_list.push_back(dist)
            dists[dist].push_back(chunk)
        if asdf > 0:
            dists_list.sort()
        near_unloaded_chunks = []
        for dist in dists_list:
            for chunk in dists[dist]:
                near_unloaded_chunks.push_back(chunk)
            
        #print("!!! sort time... ", timer.cycle())
        
        call_deferred("remove_stale_from_queue", player_chunk_coord)
    
        
    var max_free = 16
    while max_free > 0 and near_free_chunks.size() > 0:
        ensure_loaded(near_free_chunks.pop_back())
        max_free -= 1
    
    if near_unloaded_chunks.size() > 0:
        #print(near_unloaded_chunks.size())
        pass
    
    var load_budget = 0.0
    var do_time = near_unloaded_chunks.size() > 0
    var timer = Stopwatch.new()
    while near_unloaded_chunks.size() > 0:
        var chunk_coord = near_unloaded_chunks.pop_back()
        ensure_loaded(chunk_coord)
        near_unloaded_chunks_set.erase(chunk_coord)
        found = true
        if timer.stop() > load_budget:
            break
    
    if do_time:
        load_time += timer.stop()
    
    var loaded_coords = loaded_chunks.keys()
    
    if changed_pos or changed:
        for chunk_coord in loaded_coords:
            if bicone_distance(player_chunk_coord, chunk_coord)/CHUNK_SIZE > unload_distance:
                to_unload.push_back(chunk_coord)
    
    timer.cycle()
    var unload_time_budget = 0.001
    while to_unload.size() > 0:
        var chunk_coord = to_unload.pop_back()
        var chunk = chunks[chunk_coord]
        remove_chunk(chunk)
        loaded_chunks.erase(chunk_coord)
        if timer.stop() > unload_time_budget:
            break
    
    return found

class Stopwatch:
    var time0 = 0.0
    func _init():
        restart()
    func restart():
        time0 = OS.get_ticks_usec()/1000000.0
    func stop():
        return max(0.0, OS.get_ticks_usec()/1000000.0 - time0)
    func cycle():
        var ret = stop()
        restart()
        return ret

var load_time = 0.0
var remesh_time = 0.0
var landgen_time = 0.0
var fullgen_time = 0.0

const SQUISH_FACTOR = 0.9
const INNER_FACTOR = CHUNK_SIZE
static func bicone_distance(a : Vector3, b : Vector3) -> float:
    var y = abs(a.y - b.y)
    y /= SQUISH_FACTOR
    y = max(0, y-INNER_FACTOR)
    var x = Vector2(a.x, a.z).distance_to(Vector2(b.x, b.z))
    return x+y

# Called when the node enters the scene tree for the first time.
func _ready():
    #for y in range(-1, 1):
    #    for z in range(-1, 2):
    #        for x in range(-1, 2):
    #            var coord = Vector3(x, y, z)*CHUNK_SIZE
    #            ensure_loaded(coord)
    ensure_loaded(Vector3(0.0, 1.0, 0.0))
    ensure_loaded(Vector3(0.0, 0.0, 0.0))
    ensure_loaded(Vector3(0.0, -1.0, 0.0))
    for coord in loaded_chunks:
        var chunk = chunks[coord]
        chunk.remesh()
    pass # Replace with function body.


var chunk_slices = {}

func add_chunk_child(chunk : Spatial, chunk_coord : Vector3):
    if not chunk_coord.y in chunk_slices:
        var holder = Spatial.new()
        chunk_slices[chunk_coord.y] = holder
        holder.set_process(false)
        holder.set_process_input(false)
        holder.set_process_internal(false)
        holder.set_process_unhandled_input(false)
        holder.set_process_unhandled_key_input(false)
        holder.set_physics_process(false)
        holder.set_physics_process_internal(false)
        add_child(holder)
    chunk_slices[chunk_coord.y].add_child(chunk)
    pass
    
func remove_chunk(chunk : Spatial):
    if chunk.is_inside_tree():
        var holder = chunk.get_parent()
        holder.remove_child(chunk)
        if holder.get_child_count() == 0:
            for k in chunk_slices:
                if chunk_slices[k] == holder:
                    remove_child(holder)
                    holder.queue_free()
                    chunk_slices.erase(k)

func get_chunk(chunk_coord : Vector3):
    chunk_coord = to_chunk_coord(chunk_coord)
    ensure_land_generated(chunk_coord)
    return chunks[chunk_coord]

func get_chunk_full(chunk_coord : Vector3):
    chunk_coord = to_chunk_coord(chunk_coord)
    ensure_loaded(chunk_coord)
    return chunks[chunk_coord]


# Called every frame. 'delta' is the elapsed time since the previous frame.
# warning-ignore:unused_argument

func _process(delta):
    check_simulate_water(delta)
    
    $Label.text = str(Engine.get_frames_per_second())+"fps"
    
    $Label.text += "\nActive chunk count: " + str(len(loaded_chunks))
    if check_player_nearby_chunks():
        $Label.text += "\nGenerating nearby chunks... (" + str(len(near_unloaded_chunks)) + ")"
    
    var player : Spatial = get_tree().get_nodes_in_group("Player")[0]
    $Label.text += "\n" + str(player.global_translation.round())
    $Label.text += "\n" + str(to_chunk_coord(player.global_translation)/CHUNK_SIZE)
    $Label.text += "\n" + str((player.velocity*Vector3(1,0,1)).length())
    $Label.text += "\n" + str(player.velocity.y)
    $Label.text += "\nload time... " + str(stepify(load_time, 0.01))
    $Label.text += "\nlandgen time... " + str(stepify(landgen_time, 0.01))
    $Label.text += "\nfullgen time... " + str(stepify(fullgen_time, 0.01))
    $Label.text += "\nremesh time... " + str(stepify(remesh_time, 0.01))
    
    pass

const ALL_DIRS = [
    Vector3(-1, -1, -1),
    Vector3(-1, -1,  0),
    Vector3(-1, -1,  1),
    Vector3(-1,  0, -1),
    Vector3(-1,  0,  0),
    Vector3(-1,  0,  1),
    Vector3(-1,  1, -1),
    Vector3(-1,  1,  0),
    Vector3(-1,  1,  1),
    
    Vector3( 0, -1, -1),
    Vector3( 0, -1,  0),
    Vector3( 0, -1,  1),
    Vector3( 0,  0, -1),
    Vector3( 0,  0,  0),
    Vector3( 0,  0,  1),
    Vector3( 0,  1, -1),
    Vector3( 0,  1,  0),
    Vector3( 0,  1,  1),
    
    Vector3( 1, -1, -1),
    Vector3( 1, -1,  0),
    Vector3( 1, -1,  1),
    Vector3( 1,  0, -1),
    Vector3( 1,  0,  0),
    Vector3( 1,  0,  1),
    Vector3( 1,  1, -1),
    Vector3( 1,  1,  0),
    Vector3( 1,  1,  1),
]

func chunk_neighbors_loaded(chunk_coord):
    for dir in ALL_DIRS:
        if not chunk_coord + dir*CHUNK_SIZE in loaded_chunks:
            return false
    return true


# water is simulated in "ranks"
# there are 8 ranks, and for each rank, a set of horizontal slices of chunks is simulated
# so, for rank 0, chunks with y coordinates 0, size*8, etc. will be simulated
var water_rank = 0
var water_rank_count = 8
var water_timer = 0.0
var water_tick_time = 0.5
func check_simulate_water(delta):
    water_timer += delta * water_rank_count
    if water_timer >= water_tick_time:
        while water_timer > water_tick_time:
            water_timer -= water_tick_time
        water_rank = (water_rank + water_rank_count - 1) % water_rank_count
        simulate_water(delta)
    for chunk in loaded_chunks.values():
        chunk.think_always(delta)

var prev_rank = 0
func simulate_water(delta):
    var timer = Stopwatch.new()
    for chunk in loaded_chunks.values():
        var test = int(chunk.position.y / CHUNK_SIZE)
        test = ((test % water_rank_count) + water_rank_count) % water_rank_count
        if test == water_rank:
            chunk.think_step1(delta)
    for chunk in loaded_chunks.values():
        chunk.think_step2(delta)
    
