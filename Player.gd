extends QuakelikeBody


# Declare member variables here. Examples:
# var a = 2
# var b = "text"

var reflections = false

# Called when the node enters the scene tree for the first time.
#var vp_tex
func _ready():
    if reflections:
        $ReflViewport.world = get_viewport().world

class InvTile extends Reference:
    var is_block = true
    var type = 0
    var count = 1
    var stackable = false
    func _init(_is_block, _type, _count, _stackable):
        is_block = _is_block
        type = _type
        count = _count
        stackable = _stackable

var inventory_cur = 0
var inventory = []

func add_to_inventory(is_block, type, count, stackable):
    if stackable:
        for f in inventory:
            if f.type == type and f.is_block == is_block:
                f.count += count
                return true
    for i in range(inventory.size()):
        if inventory[i].type == 0:
            inventory[i] = InvTile.new(is_block, type, count, stackable)
            return true

func remove_from_inventory(index, count):
    if index < inventory.size():
        inventory[index].count -= count
        if inventory[index].count <= 0:
            inventory[index].is_block = true
            inventory[index].type = 0
            inventory[index].count = 1
            inventory[index].stackable = false

func handle_inventory():
    var first : TextureRect = $HUD/Inventory.get_child(0)
    for i in range(len($HUD/Inventory.get_children())):
        var icon : TextureRect = $HUD/Inventory.get_child(i)
        if icon != first and icon.texture == first.texture:
            icon.texture = icon.texture.duplicate()
        icon.visible = true
        if i < inventory.size() and inventory[i].type != 0:
            var label = icon.get_node("Label")
            icon.modulate.a = 1.0
            var info : GameWorld.TileType = world.tile_type_info[inventory[i].type]
            var tex : AtlasTexture = icon.texture
            tex.region.position = info.coord_side * 16.0
            label.visible = inventory[i].stackable
            label.text = str(inventory[i].count)
        else:
            icon.modulate.a = 0.0
        
        if icon.modulate.a > 0:
            icon.modulate.a = 0.5
        if inventory_cur == i:
            if icon.modulate.a > 0:
                icon.modulate.a = 1.0
            var rect = icon.get_global_rect()
            $HUD/Outline.set_global_position(rect.position, false)
            $HUD/Outline.rect_position -= Vector2(4, 4)
            $HUD/Outline.rect_size      = Vector2(4, 4)*2 + rect.size
    
    inventory_cur = ((inventory_cur+mwheel) % inventory.size() + inventory.size()) % inventory.size()
    
    mwheel = 0


var mwheel = 0
func _input(_event : InputEvent):
    if _event is InputEventMouseButton:
        var event : InputEventMouseButton = _event
        if event.pressed and event.button_index == BUTTON_WHEEL_UP:
            mwheel -= 1
        elif event.pressed and event.button_index == BUTTON_WHEEL_DOWN:
            mwheel += 1

var prev_in_water = false
var in_water = false

var floor_angle = acos(0.7)

var accel = 16.0
var walkspeed = 8.0
var wishdir = Vector3()
var velocity = Vector3()
var jumpspeed = 10.0
var gravity = -32.0
var drag = 1.0 # 1.0 = no drag, 0.5 = lose 50% speed per second
# Called every frame. 'delta' is the elapsed time since the previous frame.

var noclip = false

var window_focused = true
func _notification(what):
    if what == MainLoop.NOTIFICATION_WM_FOCUS_IN:
        window_focused = true
    elif what == MainLoop.NOTIFICATION_WM_FOCUS_OUT:
        window_focused = false

onready var world : GameWorld = get_tree().get_nodes_in_group("World")[0]
func _process(delta):
    if inventory.size() == 0:
        for i in range(11):
            inventory.push_back(InvTile.new(true, 0, 1, false))
    
    if window_focused:
        Engine.target_fps = 125
    else:
        Engine.target_fps = 12
    if reflections:
        $ReflViewport.size = get_viewport().size
    
    Engine.time_scale = 1
    
    if Input.is_action_just_pressed("ui_focus_next"):
        noclip = !noclip
    
    wishdir = Vector3()
    if Input.is_action_pressed("ui_up"):
        wishdir.z -= 1
    if Input.is_action_pressed("ui_down"):
        wishdir.z += 1
    if Input.is_action_pressed("ui_left"):
        wishdir.x -= 1
    if Input.is_action_pressed("ui_right"):
        wishdir.x += 1
    
    if wishdir.length_squared() > 1.0:
        wishdir = wishdir.normalized()
    
    var local_wishdir = wishdir.rotated(Vector3.UP, $Camera.global_rotation.y)
    
    prev_in_water = in_water
    var water_dist = distance_to_water()
    in_water = water_dist < 0
    
    $HUD/WaterOverlay.visible = water_dist < -$Camera.translation.y
    
    var actual_accel = accel
    var actual_walkspeed = walkspeed
    var actual_drag = drag
    var actual_gravity = gravity
    
    var on_floor = is_on_floor()
    
    if !on_floor:
        actual_accel *= 0.5
    if in_water:
        var temp_local_wishdir = wishdir
        temp_local_wishdir = temp_local_wishdir.rotated(Vector3.RIGHT, $Camera.global_rotation.x)
        temp_local_wishdir = temp_local_wishdir.rotated(Vector3.UP, $Camera.global_rotation.y)
        if !Input.is_action_pressed("jump") and !Input.is_key_pressed(KEY_CONTROL) and (!on_floor or temp_local_wishdir.y > 0):
            local_wishdir = temp_local_wishdir
        
        var h_part = local_wishdir
        h_part.y = 0
        var norm = max(h_part.length(), abs(local_wishdir.y))
        if norm > 0:
            local_wishdir /= norm
        
        if Input.is_action_pressed("jump"):
            local_wishdir.y = 1.0
        elif Input.is_key_pressed(KEY_CONTROL):
            local_wishdir.y = -1.0
        
        actual_accel *= 0.5
        actual_drag *= 0.1
        actual_gravity *= 0.35
        
        if local_wishdir == Vector3():
            actual_drag *= 0.2
        
        if local_wishdir.y > 0:
            detach_from_floor()
        print(water_dist)
    
    if local_wishdir == Vector3():
        actual_accel *= 0.5
    
    if in_water and local_wishdir != Vector3() and water_dist < -0.5:
        actual_gravity = 0.0
    
    if in_water and local_wishdir.y > 0.0 and water_dist > -0.5 and velocity.y > 0.0:
        local_wishdir.y *= 0.5
    
    var accel_delta = delta*actual_walkspeed*actual_accel
    var newvel_h = (velocity*Vector3(1,0,1)).move_toward(local_wishdir*actual_walkspeed*Vector3(1,0,1), accel_delta)
    var newvel_v = move_toward(velocity.y, local_wishdir.y*actual_walkspeed, accel_delta)
    
    velocity.x = newvel_h.x
    velocity.z = newvel_h.z
    if in_water and local_wishdir != Vector3() and water_dist < -0.5:
        velocity.y = newvel_v
    
    var jump_is_on_floor = on_floor
    var disable_jump = in_water
    if (prev_in_water or in_water) and (velocity.y > 0 or on_floor) and water_dist > -0.5 and is_on_wall():
        jump_is_on_floor = true
        disable_jump = false
    
    jumpspeed = 8.4
    # FIXME check if was in water last frame
    if !disable_jump and ((Input.is_action_pressed("jump") and jump_is_on_floor) or Input.is_action_just_pressed("jump")):
        velocity.y = jumpspeed
        detach_from_floor()
    
    if noclip:
        $Hull.disabled = true
        actual_gravity = 0.0
        if Input.is_action_pressed("jump"):
            velocity.y = jumpspeed
        elif Input.is_key_pressed(KEY_CONTROL):
            velocity.y = -jumpspeed
        else:
            velocity.y = 0
    else:
        $Hull.disabled = false
    
    if local_wishdir.dot(velocity) <= 0:
        velocity *= pow(actual_drag, delta/2.0)
    velocity.y += actual_gravity/2.0*delta
    velocity.y = max(velocity.y, -78.4) # terminal velocity
    
    var _oldvel = velocity
    velocity = custom_move_and_slide(delta, velocity)
    
    world.oopsie_body(self)
    
    # the flipping of the drag and gravity calculations is intentional
    # flipping it gives a CLOSE-to-framerate-independent gravity-and-drag calculation
    velocity.y += actual_gravity/2.0*delta
    if local_wishdir.dot(velocity) <= 0:
        velocity *= pow(actual_drag, delta/2.0)
    velocity.y = max(velocity.y, -78.4)
    
    check_target(delta)
    
    var water = get_tree().get_nodes_in_group("WaterPlane")[0]
    
    #$ReflectionProbe.global_translation.y = water.global_translation.y - $Camera.global_translation.y - 0.5
    $Camera.force_update_transform()
    
    if reflections:
        $ReflViewport/Camera.rotation = $Camera.rotation
        $ReflViewport/Camera.rotation.x = -$Camera.rotation.x
        $ReflViewport/Camera.global_translation = $Camera.global_translation
        $ReflViewport/Camera.global_translation.y = water.global_translation.y - $ReflViewport/Camera.global_translation.y - 0.5
        
        $ReflViewport/Camera.force_update_transform()
        
        var mat = ($"../MeshInstance2" as MeshInstance).get_active_material(0) as ShaderMaterial
        var tx = $ReflViewport.get_texture()
        tx.flags |= Texture.FLAG_FILTER
        tx.flags |= Texture.FLAG_ANISOTROPIC_FILTER
        tx.flags |= Texture.FLAG_MIPMAPS
        mat.set_shader_param("texture_reflection", tx)
    
    handle_inventory()

func get_water_level():
    var above = world.get_water(global_translation + Vector3.UP)
    if above > 0:
        var above_above = world.get_water(global_translation + Vector3.UP*2)
        if above_above > 0:
            return 2.0 + min(8, above_above)/8.0
        return 1.0 + min(8, above)/8.0 * 14.0/16.0
    var below = world.get_water(global_translation)
    return min(8, below)/8.0 * 14.0/16.0

func distance_to_water():
    var water_level = get_water_level()
    var round_coord = global_translation.round()
    var dist_to_floor = global_translation.y - round_coord.y + 0.5
    return clamp(dist_to_floor - water_level, -2, 0)

func check_target(_delta):
    var current_tile = 0
    if inventory_cur < inventory.size():
        current_tile = inventory[inventory_cur].type
    
    $Camera.far = (world.render_distance+1) * world.CHUNK_SIZE*2.0
    get_world().environment.fog_depth_begin = 0
    get_world().environment.fog_depth_end = get_viewport().get_camera().far*1.4
    $Camera.force_update_transform()
    $Camera/RayCast.force_raycast_update()
    
    var target_pos = null
    var next_pos = null
    
    var is_entity = false
    
    $Box.visible = false
    if $Camera/RayCast.get_collider():
        $Box.visible = true
        var origin = $Camera/RayCast.get_collision_point()
        var norm = $Camera/RayCast.get_collision_normal()
        origin -= norm*0.05
        origin = origin.round()
        $Box.scale = Vector3(1,1,1)
        $Box.global_translation = origin
        target_pos = origin
        next_pos = origin + norm.round()
        var ent_size = world.get_ent_size(origin)
        if ent_size != Vector3():
            $Box.scale = ent_size
            $Box.translation.y -= (1.0 - ent_size.y)/2.0
            is_entity = true
    
    if Input.is_action_just_pressed("m3"):
        if !is_entity and target_pos is Vector3:
            var tile = world.get_tile(target_pos)
            if tile != 0:
                current_tile = tile
    if Input.is_action_just_pressed("m1"):
        $Camera/A/Weapon/Animator.stop()
        $Camera/A/Weapon/Animator.play("Attack")
        if !is_entity:
            if target_pos is Vector3:
                var type = world.get_tile(target_pos)
                world.set_tile(target_pos, 0)
                
                var item : Spatial = preload("res://DroppedBlock.tscn").instance()
                item.is_block = true
                item.type = type
                world.add_child(item)
                item.global_translation = target_pos
        else:
            if target_pos is Vector3:
                world.set_ent(target_pos, 0)
    if Input.is_action_just_pressed("m2"):
        if target_pos is Vector3:
            var pos_diff = $Hull.global_translation - next_pos
            # cube boundary distance adjustment
            pos_diff.x = max(0, abs(pos_diff.x)-0.5)
            pos_diff.y = max(0, abs(pos_diff.y)-0.5)
            pos_diff.z = max(0, abs(pos_diff.z)-0.5)
            var radius = $Hull.shape.radius
            radius *= radius
            if Vector2(pos_diff.x, pos_diff.z).length_squared() > radius or pos_diff.y > $Hull.shape.height/2.0:
                remove_from_inventory(inventory_cur, 1)
                world.set_tile(next_pos, current_tile)
                $Camera/A/Weapon/Animator.stop()
                $Camera/A/Weapon/Animator.play("Attack")
        else:
            $Camera/A/Weapon/Animator.stop()
            $Camera/A/Weapon/Animator.play("Attack")
    
