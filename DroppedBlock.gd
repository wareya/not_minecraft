extends Spatial

func _ready():
    var a = randf()*PI*2
    var r = randf()*1.5
    velocity.x = sin(a)*r
    velocity.z = cos(a)*r
    velocity.y = 1.0+randf()
    
    if is_block:
        $Rect.visible = true
        $Sprite.visible = false
        var info = world.tile_type_info[type]
        if not type in world.tile_item_mats:
            var mat1 = $Rect/Top.material_override.duplicate()
            var mat2 = $Rect/Top.material_override.duplicate()
            var mat3 = $Rect/Top.material_override.duplicate()
            
            mat1.set_shader_param("tile_offset", info.coord_top)
            mat2.set_shader_param("tile_offset", info.coord_bottom)
            mat3.set_shader_param("tile_offset", info.coord_side)
            world.tile_item_mats[type] = [mat1, mat2, mat3]
        
        var mats = world.tile_item_mats[type]
        
        $Rect/Top.material_override    = mats[0]
        $Rect/Bottom.material_override = mats[1]
        $Rect/SideA.material_override  = mats[2]
        $Rect/SideB.material_override  = mats[2]
        $Rect/SideC.material_override  = mats[2]
        $Rect/SideD.material_override  = mats[2]

func move(delta, velocity):
    var local_tile = world.get_tile(global_translation)
    if local_tile != 0:
        global_translation.y += delta
        return Vector3(0,0,0)
    
    var motion = velocity*delta
    var tile = world.get_tile(global_translation + motion)
    #var info : GameWorld.TileType = world.tile_type_info[tile]
    if tile == 0:
        global_translation += motion
        return velocity
    var cell_diff = global_translation.round() - (global_translation + motion).round()
    
    # on floor
    if cell_diff.y != 0:
        motion.y = 0
        velocity.y = 0
        var mul = pow(0.5, delta)
        velocity.x *= mul
        velocity.z *= mul
        return move(delta, velocity)
    
    # wall
    if cell_diff.y == 0 and cell_diff.z == 0:
        motion.x = 0
        global_translation += motion
        velocity.x = 0
        return velocity
    
    if cell_diff.x == 0 and cell_diff.y == 0:
        motion.z = 0
        global_translation += motion
        velocity.z = 0
        return velocity
    
    # corner
    if cell_diff.y == 0:
        motion.x = 0
        motion.z = 0
        global_translation += motion
        velocity.x = 0
        velocity.z = 0
        return velocity
    
    return Vector3(0,0,0)

var lifetime_limit = 60.0*5.0

var is_block = true
var type = 65
var merge_distance_sq = 0.25
var count = 1

var rect2 = null
var sprite2 = null

var gravity = -16.0
var velocity = Vector3(0, 0, 0)
var time_alive = 0.0
onready var world : GameWorld = get_tree().get_nodes_in_group("World")[0]

var first = true
var stackable = true

func _process(delta):
    if is_queued_for_deletion():
        return
    time_alive += delta
    
    if time_alive > 1.0 and stackable:
        for _node in get_tree().get_nodes_in_group("DroppedBlock"):
            if _node == self or _node.type != type or _node.time_alive <= 1.0:
                continue
            
            var node : Spatial = _node
            if node.global_translation.distance_squared_to(global_translation) < merge_distance_sq:
                if count == 1 and is_block:
                    rect2 = $Rect.duplicate()
                    add_child(rect2)
                count += node.count
                node.queue_free()
    
    var player = get_tree().get_nodes_in_group("Player")[0]
    if time_alive > 0.25 and global_translation.distance_to(player.global_translation) < 2.0:
        player.add_to_inventory(is_block, type, count, stackable)
        queue_free()
    
    velocity.y += gravity*delta/2.0
    velocity = move(delta, velocity)
    velocity.y += gravity*delta/2.0
    
    
    $Rect.translation.y = sin(time_alive*3.0)*0.1 + 0.2
    $Rect.rotation_degrees.y += delta*45.0
    if rect2:
        rect2.transform = $Rect.transform.translated(Vector3(0.1, 0.1, 0.1))
    $Sprite.transform = $Rect.transform
    
    if time_alive > lifetime_limit:
        queue_free()

